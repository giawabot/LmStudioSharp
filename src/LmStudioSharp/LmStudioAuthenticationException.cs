using System.Net;

namespace LmStudioSharp;

public sealed class LmStudioAuthenticationException : LmStudioRequestException
{
    public LmStudioAuthenticationException(string message, HttpStatusCode statusCode, string? responseBody)
        : base(message, statusCode, responseBody)
    {
    }
}
