using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Application.Auth;

namespace Reservo.Api.Controllers;

/// <summary>Регистрация и вход.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Регистрация нового пользователя.</summary>
    /// <remarks>Сразу возвращает access-токен, отдельно входить не нужно. Новый пользователь получает роль <c>User</c>.</remarks>
    /// <response code="201">Пользователь создан.</response>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>).</response>
    /// <response code="409">Email уже занят (<c>auth.email_taken</c>).</response>
    [HttpPost("register")]
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
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        authService.LoginAsync(request, cancellationToken);
}
