namespace Reservo.Application.Common.Errors;

/// <summary>
/// What kind of failure an <see cref="AppException"/> is.
/// The API layer maps it to an HTTP status code, so the Application layer stays free of HTTP details.
/// </summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}
