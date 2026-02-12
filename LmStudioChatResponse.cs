using System.Text.Json.Serialization;

namespace LmStudioSharp;

public sealed class LmStudioChatResponse
{
    [JsonPropertyName("model_instance_id")]
    public string? ModelInstanceId { get; set; }

    [JsonPropertyName("output")]
    public List<LmStudioOutput> Output { get; set; } = [];

    [JsonPropertyName("stats")]
    public LmStudioStats? Stats { get; set; }

    [JsonPropertyName("response_id")]
    public string? ResponseId { get; set; }
}

public sealed class LmStudioOutput
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class LmStudioStats
{
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; set; }

    [JsonPropertyName("total_output_tokens")]
    public int? TotalOutputTokens { get; set; }

    [JsonPropertyName("reasoning_output_tokens")]
    public int? ReasoningOutputTokens { get; set; }

    [JsonPropertyName("tokens_per_second")]
    public decimal? TokensPerSecond { get; set; }

    [JsonPropertyName("time_to_first_token_seconds")]
    public decimal? TimeToFirstTokenSeconds { get; set; }

    [JsonPropertyName("model_load_time_seconds")]
    public decimal? ModelLoadTimeSeconds { get; set; }
}

public sealed record LmStudioChatResult(
    string Output,
    string Reasoning,
    string? ResponseId);