namespace LmStudioSharp;

public sealed class LmStudioClientOptions
{
    public string Host { get; set; } = "";

    public string BearerToken { get; set; } = "";

    public bool AllowInsecureHttp { get; set; }

    public int MaxResponseBodyBytes { get; set; } = 32 * 1024 * 1024;

    public int MaxSseEventBytes { get; set; } = 16 * 1024 * 1024;
}

