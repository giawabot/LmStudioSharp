using System.Text.Json.Serialization;

namespace LmStudioSharp;

public abstract class LmStudioInputItem
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public sealed class LmStudioTextInput : LmStudioInputItem
{
    [JsonPropertyName("type")]
    public override string Type => "text";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    public LmStudioTextInput()
    {
    }

    public LmStudioTextInput(string content)
    {
        Content = content;
    }
}

public sealed class LmStudioImageInput : LmStudioInputItem
{
    [JsonPropertyName("type")]
    public override string Type => "image";

    [JsonPropertyName("data_url")]
    public string DataUrl { get; set; } = "";

    public LmStudioImageInput()
    {
    }

    public LmStudioImageInput(string dataUrl)
    {
        DataUrl = dataUrl;
    }
}
