using System.Net;

namespace ExercisesTestAPI.Services;

public sealed class SapServiceLayerException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public SapServiceLayerException(string message, HttpStatusCode statusCode, string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
