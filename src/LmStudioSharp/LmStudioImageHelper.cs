namespace LmStudioSharp;

public static class LmStudioImageHelper
{
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".gif", "image/gif" }
    };

    public static LmStudioImageInput LoadImage(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Image file not found.", filePath);
        }

        var extension = Path.GetExtension(filePath);
        if (!MimeTypes.TryGetValue(extension, out var mimeType))
        {
            throw new NotSupportedException($"Image format '{extension}' is not supported. Supported formats: {string.Join(", ", MimeTypes.Keys)}");
        }

        var bytes = File.ReadAllBytes(filePath);
        var base64 = Convert.ToBase64String(bytes);
        var dataUrl = $"data:{mimeType};base64,{base64}";

        return new LmStudioImageInput(dataUrl);
    }

    public static async Task<LmStudioImageInput> LoadImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Image file not found.", filePath);
        }

        var extension = Path.GetExtension(filePath);
        if (!MimeTypes.TryGetValue(extension, out var mimeType))
        {
            throw new NotSupportedException($"Image format '{extension}' is not supported. Supported formats: {string.Join(", ", MimeTypes.Keys)}");
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var base64 = Convert.ToBase64String(bytes);
        var dataUrl = $"data:{mimeType};base64,{base64}";

        return new LmStudioImageInput(dataUrl);
    }

    public static LmStudioImageInput FromBase64(string base64, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new ArgumentException("Base64 string cannot be null or empty.", nameof(base64));
        }

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new ArgumentException("MIME type cannot be null or empty.", nameof(mimeType));
        }

        var dataUrl = $"data:{mimeType};base64,{base64}";
        return new LmStudioImageInput(dataUrl);
    }
}
