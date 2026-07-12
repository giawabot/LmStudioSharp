using System.Text.Json;
using System.Text.Json.Serialization;

namespace LmStudioSharp;

public sealed class LmStudioRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("system_prompt")]
    public string SystemPrompt { get; set; } = "";

    [JsonPropertyName("input")]
    [JsonConverter(typeof(LmStudioInputConverter))]
    public object? Input { get; set; } = "";

    [JsonPropertyName("integrations")]
    public string[]? Integrations { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    [JsonPropertyName("temperature")]
    public decimal Temperature { get; set; } = 0.7m;

    [JsonPropertyName("repeat_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? RepeatPenalty { get; set; }

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

public sealed class LmStudioInputConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var items = new List<LmStudioInputItem>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    var item = ReadInputItem(ref reader);
                    if (item is not null)
                    {
                        items.Add(item);
                    }
                }
            }
            return items;
        }

        return null;
    }

    private static LmStudioInputItem? ReadInputItem(ref Utf8JsonReader reader)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProperty))
        {
            return null;
        }

        var type = typeProperty.GetString();
        var rawText = root.GetRawText();

        return type switch
        {
            "text" => JsonSerializer.Deserialize<LmStudioTextInput>(rawText),
            "image" => JsonSerializer.Deserialize<LmStudioImageInput>(rawText),
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value is string stringValue)
        {
            writer.WriteStringValue(stringValue);
        }
        else if (value is List<LmStudioInputItem> items)
        {
            writer.WriteStartArray();
            foreach (var item in items)
            {
                JsonSerializer.Serialize(writer, item, item.GetType(), options);
            }
            writer.WriteEndArray();
        }
        else if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            throw new JsonException($"Unsupported input type: {value.GetType()}");
        }
    }
}

