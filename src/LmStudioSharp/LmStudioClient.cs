using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LmStudioSharp;

public sealed class LmStudioClient : IDisposable
{
    private const string ChatEndpoint = "/api/v1/chat";
    private const string ModelsEndpoint = "/api/v1/models";
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly bool _ownsHttpClient;
    private readonly int _maxResponseBodyBytes;
    private readonly int _maxSseEventBytes;
    private bool _disposed;

    /// <summary>
    /// Initializes a new client for LM Studio using the provided options.
    /// </summary>
    /// <param name="options">Configuration including host URL and optional bearer token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options.Host"/> is empty.</exception>
    public LmStudioClient(LmStudioClientOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// Initializes a new client for LM Studio using the provided options and <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="options">Configuration including host URL and optional bearer token.</param>
    /// <param name="httpClient">
    /// Optional <see cref="HttpClient"/> to use. If provided and missing a <see cref="HttpClient.BaseAddress"/>,
    /// it will be set to <paramref name="options"/>'s host.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options.Host"/> is empty.</exception>
    public LmStudioClient(LmStudioClientOptions options, HttpClient? httpClient)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new ArgumentException("LM Studio host is required.", nameof(options));
        }

        var optionsHostUri = new Uri(options.Host, UriKind.Absolute);

        if (options.MaxResponseBodyBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxResponseBodyBytes must be greater than zero.");
        }

        if (options.MaxSseEventBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxSseEventBytes must be greater than zero.");
        }

        if (httpClient is null)
        {
            EnsureAllowedBaseAddress(optionsHostUri, options.AllowInsecureHttp);
            _httpClient = new HttpClient
            {
                BaseAddress = optionsHostUri,
                Timeout = Timeout.InfiniteTimeSpan,
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            if (_httpClient.BaseAddress is null)
            {
                EnsureAllowedBaseAddress(optionsHostUri, options.AllowInsecureHttp);
                _httpClient.BaseAddress = optionsHostUri;
            }
            else
            {
                EnsureAllowedBaseAddress(_httpClient.BaseAddress, options.AllowInsecureHttp);
            }
        }

        if (!string.IsNullOrWhiteSpace(options.BearerToken) &&
            _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.BearerToken);
        }

        _serializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        _maxResponseBodyBytes = options.MaxResponseBodyBytes;
        _maxSseEventBytes = options.MaxSseEventBytes;
    }

    /// <summary>
    /// Streams a chat response from LM Studio as Server-Sent Events.
    /// </summary>
    /// <param name="request">The chat request payload. Set <see cref="LmStudioRequest.Stream"/> to true.</param>
    /// <param name="cancellationToken">Cancellation token to abort the request or stream.</param>
    /// <returns>An async stream of <see cref="LmStudioStreamChunk"/> values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="LmStudioRequestException">Thrown on HTTP or protocol failures.</exception>
    public async IAsyncEnumerable<LmStudioStreamChunk> StreamAsync(
        LmStudioRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await foreach (var chunk in StreamCoreAsync(request, allowReasoningRetry: true, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Executes a non-streaming chat request and returns the flattened output and reasoning.
    /// </summary>
    /// <param name="request">The chat request payload. The client will force non-streaming for this call.</param>
    /// <param name="cancellationToken">Cancellation token to abort the request.</param>
    /// <returns>The combined output and reasoning, or null if the response cannot be parsed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="LmStudioAuthenticationException">Thrown when authentication is required or invalid.</exception>
    public async Task<LmStudioChatResult?> RunAsync(
        LmStudioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var response = await RunForResponseAsync(request, cancellationToken);
        if (response is null)
        {
            return null;
        }

        var outputBuilder = new StringBuilder();
        var reasoningBuilder = new StringBuilder();
        foreach (var output in response.Output)
        {
            if (!HasSegmentContent(output.Content))
            {
                continue;
            }

            if (string.Equals(output.Type, "reasoning", StringComparison.OrdinalIgnoreCase))
            {
                reasoningBuilder.Append(output.Content);
            }
            else
            {
                outputBuilder.Append(output.Content);
            }
        }

        return new LmStudioChatResult(
            outputBuilder.ToString(),
            reasoningBuilder.ToString(),
            response.ResponseId);
    }

    /// <summary>
    /// Executes a non-streaming chat request and returns the raw response including stats.
    /// </summary>
    /// <param name="request">The chat request payload. The client will force non-streaming for this call.</param>
    /// <param name="cancellationToken">Cancellation token to abort the request.</param>
    /// <returns>The raw chat response, or null if the response cannot be parsed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="LmStudioAuthenticationException">Thrown when authentication is required or invalid.</exception>
    public async Task<LmStudioChatResponse?> RunForResponseAsync(
        LmStudioRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nonStreamingRequest = request.Stream
            ? CloneRequest(request)
            : request;
        nonStreamingRequest.Stream = false;

        return await RunResponseCoreAsync(nonStreamingRequest, cancellationToken, allowReasoningRetry: true);
    }

    /// <summary>
    /// Retrieves the list of available models from the LM Studio server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the request.</param>
    /// <returns>The models response, or null if the payload cannot be parsed.</returns>
    /// <exception cref="LmStudioRequestException">Thrown on HTTP failures.</exception>
    /// <exception cref="LmStudioAuthenticationException">Thrown when authentication is required or invalid.</exception>
    public async Task<LmStudioModelsResponse?> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(ModelsEndpoint, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new LmStudioRequestException("Request failed.", exception);
        }

        using var responseScope = response;
        string responseBody;
        try
        {
            responseBody = await ReadResponseBodyAsync(response.Content, cancellationToken);
        }
        catch (LmStudioRequestException)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            ThrowIfAuthenticationError(responseBody, response.StatusCode);
            throw new LmStudioRequestException(
                $"Request failed with {(int)response.StatusCode} ({response.ReasonPhrase}).",
                response.StatusCode,
                responseBody);
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LmStudioModelsResponse>(responseBody, _serializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Disposes the underlying <see cref="HttpClient"/> if owned by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }

    private async IAsyncEnumerable<LmStudioStreamChunk> StreamCoreAsync(
        LmStudioRequest request,
        bool allowReasoningRetry,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(request, _serializerOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, ChatEndpoint) { Content = content },
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new LmStudioRequestException("Request failed.", exception);
        }

        using var responseScope = response;
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await ReadResponseBodyAsync(response.Content, cancellationToken);
            ThrowIfAuthenticationError(responseBody, response.StatusCode);
            if (allowReasoningRetry &&
                request.Reasoning is not null &&
                ShouldRetryWithoutReasoning(responseBody))
            {
                var retryRequest = CloneRequest(request);
                retryRequest.Reasoning = null;
                await foreach (var chunk in StreamCoreAsync(retryRequest, allowReasoningRetry: false, cancellationToken))
                {
                    yield return chunk;
                }

                yield break;
            }

            throw new LmStudioRequestException(
                $"Request failed with {(int)response.StatusCode} ({response.ReasonPhrase}).",
                response.StatusCode,
                responseBody);
        }

        string? responseId = null;
        var responseHandled = false;
        string? lastChunk = null;
        var repeatChunkCount = 0;
        string? currentEventType = null;
        var currentEventData = new StringBuilder();
        var currentEventDataBytes = 0;
        var streamedAnySegments = false;
        var stoppedDueToRepeatingChunk = false;

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                if (currentEventData.Length > 0 || !string.IsNullOrWhiteSpace(currentEventType))
                {
                    var eventData = currentEventData.ToString();
                    if (!string.IsNullOrWhiteSpace(eventData))
                    {
                        if (string.Equals(eventData, "[DONE]", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        if (HandleSseEvent(currentEventType, eventData, ref responseId, streamedAnySegments, out var chunks, out var streamedNow))
                        {
                            foreach (var chunk in chunks)
                            {
                                yield return chunk;
                            }

                            if (streamedNow)
                            {
                                streamedAnySegments = true;
                            }

                            responseHandled = true;
                        }

                        if (ShouldStopForRepeatingChunk(eventData, ref lastChunk, ref repeatChunkCount))
                        {
                            yield return new LmStudioStreamChunk(
                                LmStudioStreamChunkKind.Warning,
                                "Detected repeating output chunks. Stopping stream.");
                            stoppedDueToRepeatingChunk = true;
                            break;
                        }
                    }

                    currentEventType = null;
                    currentEventData.Clear();
                    currentEventDataBytes = 0;
                }

                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                currentEventType = line[6..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var dataSegment = StripSseDataPrefixWhitespace(line);
                var addedBytes = Utf8NoBom.GetByteCount(dataSegment);

                if (currentEventData.Length > 0)
                {
                    currentEventData.AppendLine();
                    addedBytes += Utf8NoBom.GetByteCount(Environment.NewLine);
                }

                currentEventDataBytes += addedBytes;
                if (currentEventDataBytes > _maxSseEventBytes)
                {
                    throw new LmStudioRequestException(
                        $"SSE event exceeded max size of {_maxSseEventBytes} bytes.");
                }

                currentEventData.Append(dataSegment);
                continue;
            }

            if (line.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                if (Utf8NoBom.GetByteCount(line) > _maxSseEventBytes)
                {
                    throw new LmStudioRequestException(
                        $"SSE payload exceeded max size of {_maxSseEventBytes} bytes.");
                }

                if (TryHandleFullResponse(line, ref responseId, out var fullChunks, out var streamedNow))
                {
                    foreach (var chunk in fullChunks)
                    {
                        yield return chunk;
                    }

                    if (streamedNow)
                    {
                        streamedAnySegments = true;
                    }

                    responseHandled = true;
                    break;
                }
            }
        }

        if (!responseHandled && !streamedAnySegments && !stoppedDueToRepeatingChunk)
        {
            yield return new LmStudioStreamChunk(
                LmStudioStreamChunkKind.Warning,
                "Streaming response did not include any payload data.");
        }
    }

    private bool TryHandleFullResponse(
        string responseBody,
        ref string? responseId,
        out List<LmStudioStreamChunk> chunks,
        out bool streamedSegments)
    {
        chunks = [];
        streamedSegments = false;

        var responsePayload = JsonSerializer.Deserialize<LmStudioChatResponse>(responseBody, _serializerOptions);
        if (responsePayload is null)
        {
            return false;
        }

        foreach (var chunk in ConvertOutputs(responsePayload.Output))
        {
            chunks.Add(chunk);
            if (chunk.Kind is LmStudioStreamChunkKind.Output or LmStudioStreamChunkKind.Reasoning)
            {
                streamedSegments = true;
            }
        }

        if (TrySetResponseId(responsePayload.ResponseId, ref responseId, out var responseChunk))
        {
            chunks.Add(responseChunk);
        }

        return true;
    }

    private bool HandleSseEvent(
        string? eventType,
        string json,
        ref string? responseId,
        bool hasStreamedSegments,
        out List<LmStudioStreamChunk> chunks,
        out bool streamedSegments)
    {
        chunks = [];
        streamedSegments = false;

        try
        {
            var payload = JsonSerializer.Deserialize<LmStudioSsePayload>(json, _serializerOptions);
            if (payload is null)
            {
                return false;
            }

            var normalizedEventType = NormalizeEventType(eventType, payload);

            if (string.Equals(normalizedEventType, "chat.end", StringComparison.OrdinalIgnoreCase))
            {
                if (payload.Result is not null)
                {
                    if (TrySetResponseId(payload.Result.ResponseId, ref responseId, out var responseChunk))
                    {
                        chunks.Add(responseChunk);
                    }

                    if (!hasStreamedSegments)
                    {
                        foreach (var chunk in ConvertOutputs(payload.Result.Output))
                        {
                            chunks.Add(chunk);
                            if (chunk.Kind is LmStudioStreamChunkKind.Output or LmStudioStreamChunkKind.Reasoning)
                            {
                                streamedSegments = true;
                            }
                        }
                    }

                    return true;
                }

                return false;
            }

            if (string.Equals(normalizedEventType, "reasoning.delta", StringComparison.OrdinalIgnoreCase))
            {
                if (HasSegmentContent(payload.Content))
                {
                    chunks.Add(new LmStudioStreamChunk(
                        LmStudioStreamChunkKind.Reasoning,
                        payload.Content));
                    streamedSegments = true;
                    return true;
                }

                return false;
            }

            if (string.Equals(normalizedEventType, "message.delta", StringComparison.OrdinalIgnoreCase))
            {
                if (HasSegmentContent(payload.Content))
                {
                    chunks.Add(new LmStudioStreamChunk(
                        LmStudioStreamChunkKind.Output,
                        payload.Content));
                    streamedSegments = true;
                    return true;
                }

                return false;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<LmStudioChatResponse?> RunResponseCoreAsync(
        LmStudioRequest request,
        CancellationToken cancellationToken,
        bool allowReasoningRetry)
    {
        var payload = JsonSerializer.Serialize(request, _serializerOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, ChatEndpoint) { Content = content },
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        using var responseScope = response;
        var responseBody = await ReadResponseBodyAsync(response.Content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ThrowIfAuthenticationError(responseBody, response.StatusCode);
            if (allowReasoningRetry &&
                request.Reasoning is not null &&
                ShouldRetryWithoutReasoning(responseBody))
            {
                var retryRequest = CloneRequest(request);
                retryRequest.Reasoning = null;
                return await RunResponseCoreAsync(retryRequest, cancellationToken, allowReasoningRetry: false);
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        LmStudioChatResponse? responsePayload;
        try
        {
            responsePayload = JsonSerializer.Deserialize<LmStudioChatResponse>(responseBody, _serializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (responsePayload is null)
        {
            return null;
        }

        return responsePayload;
    }

    private static bool TrySetResponseId(
        string? newResponseId,
        ref string? currentResponseId,
        out LmStudioStreamChunk responseChunk)
    {
        responseChunk = default;
        if (string.IsNullOrWhiteSpace(newResponseId) ||
            string.Equals(currentResponseId, newResponseId, StringComparison.Ordinal))
        {
            return false;
        }

        currentResponseId = newResponseId;
        responseChunk = new LmStudioStreamChunk(
            LmStudioStreamChunkKind.ResponseId,
            null,
            newResponseId);
        return true;
    }

    private static IEnumerable<LmStudioStreamChunk> ConvertOutputs(IEnumerable<LmStudioOutput> outputs)
    {
        foreach (var output in outputs)
        {
            if (!HasSegmentContent(output.Content))
            {
                continue;
            }

            var kind = string.Equals(output.Type, "reasoning", StringComparison.OrdinalIgnoreCase)
                ? LmStudioStreamChunkKind.Reasoning
                : LmStudioStreamChunkKind.Output;

            yield return new LmStudioStreamChunk(kind, output.Content);
        }
    }

    private static string? NormalizeEventType(string? eventType, LmStudioSsePayload payload)
    {
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            return eventType;
        }

        if (!string.IsNullOrWhiteSpace(payload.Type))
        {
            return payload.Type;
        }

        return null;
    }

    private static bool ShouldStopForRepeatingChunk(string rawText, ref string? previousChunk, ref int repeatCount)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        if (string.Equals(previousChunk, rawText, StringComparison.Ordinal))
        {
            repeatCount++;
        }
        else
        {
            repeatCount = 0;
            previousChunk = rawText;
        }

        return repeatCount >= 5;
    }

    private static string StripSseDataPrefixWhitespace(string line)
    {
        var data = line[5..];
        return data.StartsWith(' ') ? data[1..] : data;
    }

    private bool ShouldRetryWithoutReasoning(string? responseBody)
    {
        var error = TryParseError(responseBody);
        if (error is null)
        {
            return responseBody?.IndexOf("reasoning", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (string.Equals(error.Param, "reasoning", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return error.Message?.IndexOf("reasoning", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static LmStudioRequest CloneRequest(LmStudioRequest request)
    {
        var clone = new LmStudioRequest
        {
            Model = request.Model,
            SystemPrompt = request.SystemPrompt,
            Reasoning = request.Reasoning,
            Temperature = request.Temperature,
            RepeatPenalty = request.RepeatPenalty,
            MaxOutputTokens = request.MaxOutputTokens,
            ContextLength = request.ContextLength,
            PreviousResponseId = request.PreviousResponseId,
            TopP = request.TopP,
            Stream = request.Stream,
        };

        if (request.Input is string stringInput)
        {
            clone.Input = stringInput;
        }
        else if (request.Input is List<LmStudioInputItem> items)
        {
            clone.Input = new List<LmStudioInputItem>(items);
        }

        if (request.Integrations is not null)
        {
            clone.Integrations = [.. request.Integrations];
        }

        return clone;
    }

    internal static bool HasSegmentContent(string? content)
    {
        return !string.IsNullOrEmpty(content);
    }

    private void ThrowIfAuthenticationError(string? responseBody, HttpStatusCode statusCode)
    {
        if (statusCode != HttpStatusCode.Unauthorized &&
            statusCode != HttpStatusCode.Forbidden)
        {
            return;
        }

        var error = TryParseError(responseBody);
        if (error is null)
        {
            return;
        }

        if (string.Equals(error.Code, "invalid_api_key", StringComparison.OrdinalIgnoreCase))
        {
            throw new LmStudioAuthenticationException(
                "LM Studio API authentication failed. Provide a valid bearer token.",
                statusCode,
                responseBody);
        }
    }

    private async Task<string> ReadResponseBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long contentLength &&
            contentLength > _maxResponseBodyBytes)
        {
            throw new LmStudioRequestException(
                $"Response body exceeded max size of {_maxResponseBodyBytes} bytes.");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        while (true)
        {
            var bytesRead = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            if (buffer.Length + bytesRead > _maxResponseBodyBytes)
            {
                throw new LmStudioRequestException(
                    $"Response body exceeded max size of {_maxResponseBodyBytes} bytes.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }

        return Utf8NoBom.GetString(buffer.ToArray());
    }

    private static void EnsureAllowedBaseAddress(Uri baseAddress, bool allowInsecureHttp)
    {
        if (string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "LM Studio host must use http or https.",
                nameof(baseAddress));
        }

        if (allowInsecureHttp || IsLoopbackHost(baseAddress))
        {
            return;
        }

        throw new ArgumentException(
            "HTTP is only allowed for loopback hosts by default. Set AllowInsecureHttp=true to allow non-loopback HTTP.",
            nameof(baseAddress));
    }

    private static bool IsLoopbackHost(Uri uri)
    {
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(uri.Host, out var address))
        {
            return IPAddress.IsLoopback(address);
        }

        return false;
    }

    private LmStudioClientError? TryParseError(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            var response = JsonSerializer.Deserialize<LmStudioClientErrorResponse>(responseBody, _serializerOptions);
            return response?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record LmStudioSsePayload(
        string? Type,
        string? Content,
        LmStudioChatResponse? Result);
}

