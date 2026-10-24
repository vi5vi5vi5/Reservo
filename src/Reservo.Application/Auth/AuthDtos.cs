using FluentValidation;
using Reservo.Application.Users;

namespace Reservo.Application.Auth;

/// <summary>Данные для регистрации.</summary>
/// <param name="Email">Email, он же логин. Регистр не важен.</param>
/// <param name="Password">Пароль: от 8 до 128 символов, минимум одна буква и одна цифра.</param>
/// <param name="FullName">Имя и фамилия.</param>
public sealed record RegisterRequest(string Email, string Password, string FullName);

/// <summary>Данные для входа.</summary>
/// <param name="Email">Email, указанный при регистрации.</param>
/// <param name="Password">Пароль.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Refresh-токен для обмена на новую пару токенов или для выхода.</summary>
/// <param name="RefreshToken">Токен из ответа на вход, регистрацию или предыдущий обмен.</param>
public sealed record RefreshTokenRequest(string RefreshToken);

/// <summary>Результат успешной регистрации, входа или обмена refresh-токена.</summary>
/// <param name="AccessToken">JWT. Передаётся в заголовке <c>Authorization: Bearer &lt;токен&gt;</c>, живёт 30 минут.</param>
/// <param name="ExpiresAt">Когда access-токен перестанет действовать (UTC).</param>
/// <param name="RefreshToken">Долгоживущий токен для <c>POST /api/auth/refresh</c>. Одноразовый: при обмене выдаётся новый.</param>
/// <param name="RefreshTokenExpiresAt">Когда refresh-токен перестанет действовать (UTC).</param>
/// <param name="User">Профиль вошедшего пользователя.</param>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Передайте refresh-токен.")
            .MaximumLength(200).WithMessage("Refresh-токен слишком длинный.");
    }
}
