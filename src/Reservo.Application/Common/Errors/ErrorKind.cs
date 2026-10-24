namespace Reservo.Application.Common.Errors;

/// <summary>
/// Вид ошибки <see cref="AppException"/>.
/// HTTP-статус по нему выбирает слой API, поэтому слой Application ничего не знает про HTTP.
/// </summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}
