# LmStudioSharp

Lightweight .NET client for LM Studio's chat API. This library is transport-only: it manages HTTP, SSE streaming, and response parsing. Prompts, tool routing, and app-specific behavior should live in the calling application.

LM Studio REST API docs:
```text
https://lmstudio.ai/docs/developer/rest
```

## Key Concepts
- **Streaming**: Use `StreamAsync` to receive `LmStudioStreamChunk` values as the model responds.
- **Response IDs**: A `ResponseId` chunk is emitted when the server includes an ID.
- **Warnings**: The client emits a warning chunk if the stream repeats output chunks or yields no payload.

## Authentication
Bearer tokens are only required if your LM Studio server requires authentication. If enabled, set `BearerToken` in `LmStudioClientOptions` and handle `LmStudioAuthenticationException` for missing/invalid tokens.

## Transport Security
- Prefer `https://` hosts.
- `http://` is allowed by default only for loopback hosts (`localhost`, `127.0.0.1`, `::1`).
- For non-loopback HTTP hosts (for example `http://192.168.0.123`), set `AllowInsecureHttp = true` in `LmStudioClientOptions`.

## Basic Usage
```csharp
using LmStudioSharp;

var options = new LmStudioClientOptions
{
    Host = "http://localhost:1234",
    BearerToken = "",
};

using var client = new LmStudioClient(options);

var request = new LmStudioRequest
{
    Model = "zai-org/glm-4.7-flash",
    SystemPrompt = "You are helpful.",
    Input = "Hello!",
    Stream = true,
};

await foreach (var chunk in client.StreamAsync(request))
{
    switch (chunk.Kind)
    {
        case LmStudioStreamChunkKind.Reasoning:
        case LmStudioStreamChunkKind.Output:
            Console.Write(chunk.Content);
            break;
        case LmStudioStreamChunkKind.ResponseId:
            Console.WriteLine($"\nResponse ID: {chunk.ResponseId}");
            break;
        case LmStudioStreamChunkKind.Warning:
            Console.WriteLine($"\n[warning] {chunk.Content}");
            break;
    }
}
```

## Streaming Contract
- `StreamAsync` yields chunks in the order they are received from LM Studio.
- `Reasoning` and `Output` chunks may interleave depending on the model.
- `Warning` chunks indicate unusual conditions (e.g., repeating output or no payload).

## Non-Streaming Usage
```csharp
request.Stream = false;
var result = await client.RunAsync(request);
if (result is not null)
{
    Console.WriteLine(result.Output);
}
```

If you need access to the raw response (including stats), use `RunForResponseAsync`:
```csharp
request.Stream = false;
var response = await client.RunForResponseAsync(request);
if (response is not null)
{
    Console.WriteLine(response.Stats?.TotalOutputTokens);
}
```

## Listing Models
```csharp
var models = await client.GetModelsAsync();
foreach (var model in models?.Models ?? [])
{
    Console.WriteLine($"{model.DisplayName} ({model.Key})");
}
```

## Error Handling
- `StreamAsync` throws `LmStudioRequestException` on HTTP failures.
- `RunAsync` returns `null` when the response cannot be parsed or the request fails.
- `RunAsync`, `RunForResponseAsync`, and `GetModelsAsync` throw `LmStudioAuthenticationException` for missing/invalid bearer tokens.
- `GetModelsAsync` throws `LmStudioRequestException` on HTTP failures.

## Notes
- Use the app layer to implement prompts, tool routing, and any persistence.
- Inject your own `HttpClient` if you need custom handlers or lifetime management.

## Development Note
Portions of this project were developed with LLMs such as Codex 5.2, GLM 4.7 and Qwen3

## Contributing
Pull requests and issues are open and accepted on this project.
