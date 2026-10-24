using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Application.Auth;

namespace Reservo.Api.Controllers;

/// <summary>Регистрация, вход и сессии.</summary>
/// <remarks>
/// Сессия — это пара токенов: access-токен (JWT, 30 минут) для запросов и refresh-токен (14 дней),
/// которым access-токен обновляется без пароля через <c>POST /api/auth/refresh</c>.
/// </remarks>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Регистрация нового пользователя.</summary>
    /// <remarks>Сразу возвращает пару токенов, отдельно входить не нужно. Новый пользователь получает роль <c>User</c>.</remarks>
    /// <response code="201">Пользователь создан.</response>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>).</response>
    /// <response code="409">Email уже занят (<c>auth.email_taken</c>).</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.RegisterAsync(request, cancellationToken);
        return Created("/api/users/me", response);
    }

    /// <summary>Вход по email и паролю.</summary>
    /// <response code="200">Успешный вход.</response>
    /// <response code="400">Не заполнены email или пароль (<c>validation_failed</c>).</response>
    /// <response code="401">Неверный email или пароль (<c>auth.invalid_credentials</c>).</response>
    /// <response code="403">Учётная запись заблокирована (<c>auth.user_blocked</c>).</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        authService.LoginAsync(request, cancellationToken);

    /// <summary>Обменять refresh-токен на новую пару токенов.</summary>
    /// <remarks>
    /// Refresh-токен одноразовый: после обмена старый перестаёт работать. Если отозванный токен прислать ещё раз,
    /// все сессии пользователя закрываются — так защищаемся от кражи токена.
    /// </remarks>
    /// <response code="200">Новая пара токенов.</response>
    /// <response code="401">Токен неизвестен, истёк или отозван (<c>auth.invalid_refresh_token</c>).</response>
    /// <response code="403">Учётная запись заблокирована (<c>auth.user_blocked</c>).</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<AuthResponse> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        authService.RefreshAsync(request, cancellationToken);

    /// <summary>Выйти: отозвать refresh-токен текущей сессии.</summary>
    /// <remarks>Access-токен перестанет действовать сам через несколько минут. Повторный выход — не ошибка.</remarks>
    /// <response code="204">Сессия закрыта.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
