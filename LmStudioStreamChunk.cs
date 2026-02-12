namespace LmStudioSharp;

public enum LmStudioStreamChunkKind
{
    Reasoning,
    Output,
    ResponseId,
    Warning,
}

public readonly record struct LmStudioStreamChunk(
    LmStudioStreamChunkKind Kind,
    string? Content,
    string? ResponseId = null);

