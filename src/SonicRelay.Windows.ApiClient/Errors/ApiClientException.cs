using System.Net;

namespace SonicRelay.Windows.ApiClient.Errors;

public enum ApiErrorKind
{
    Unauthorized,
    Forbidden,
    Validation,
    Conflict,
    NetworkUnavailable,
    BackendUnavailable,
    Unknown
}

public sealed class ApiClientException(
    ApiErrorKind kind,
    string message,
    HttpStatusCode? statusCode = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public ApiErrorKind Kind { get; } = kind;
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
