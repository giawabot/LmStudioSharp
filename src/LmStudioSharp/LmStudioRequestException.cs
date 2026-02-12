using System.Net;

namespace LmStudioSharp;

public class LmStudioRequestException : Exception
{
    public LmStudioRequestException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public LmStudioRequestException(
        string message,
        HttpStatusCode statusCode,
        string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ResponseBody { get; }
}

