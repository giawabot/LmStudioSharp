using System.Text.Json.Serialization;

namespace LmStudioSharp;

public sealed class LmStudioClientErrorResponse
{
    [JsonPropertyName("error")]
    public LmStudioClientError? Error { get; set; }
}

public sealed class LmStudioClientError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("param")]
    public string? Param { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
