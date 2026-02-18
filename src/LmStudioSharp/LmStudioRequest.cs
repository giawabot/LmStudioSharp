using System.Text.Json.Serialization;

namespace LmStudioSharp;

public sealed class LmStudioRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("system_prompt")]
    public string SystemPrompt { get; set; } = "";

    [JsonPropertyName("input")]
    public string Input { get; set; } = "";

    [JsonPropertyName("integrations")]
    public string[]? Integrations { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; } = "on";

    [JsonPropertyName("temperature")]
    public decimal Temperature { get; set; } = 0.7m;

    [JsonPropertyName("repeat_penalty")]
    public decimal RepeatPenalty { get; set; } = 0m;

    [JsonPropertyName("max_output_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MaxOutputTokens { get; set; } = 0;

    [JsonPropertyName("top_p")]
    public decimal TopP { get; set; } = 0.95m;

    [JsonPropertyName("context_length")]
    public int ContextLength { get; set; } = 16384;

    [JsonPropertyName("previous_response_id")]
    public string? PreviousResponseId { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

