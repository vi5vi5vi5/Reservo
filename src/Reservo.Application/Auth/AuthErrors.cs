using Reservo.Application.Common.Errors;

namespace Reservo.Application.Auth;

/// <summary>All authentication errors in one place (codes from PROJECT.md, 7.3).</summary>
public static class AuthErrors
{
    public static AppException InvalidCredentials() =>
        new(ErrorKind.Unauthorized, "auth.invalid_credentials", "Неверный email или пароль.");

    public static AppException EmailTaken() =>
        new(ErrorKind.Conflict, "auth.email_taken", "Пользователь с таким email уже зарегистрирован.");

    public static AppException UserBlocked() =>
        new(ErrorKind.Forbidden, "auth.user_blocked", "Учётная запись заблокирована. Обратитесь к администратору.");
}
