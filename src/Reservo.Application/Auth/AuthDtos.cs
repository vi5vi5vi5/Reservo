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

/// <summary>Результат успешной регистрации или входа.</summary>
/// <param name="AccessToken">JWT. Передаётся в заголовке <c>Authorization: Bearer &lt;токен&gt;</c>.</param>
/// <param name="ExpiresAt">Когда токен перестанет действовать (UTC).</param>
/// <param name="User">Профиль вошедшего пользователя.</param>
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
