using Microsoft.AspNetCore.Mvc;
using Reservo.Application.Users;

namespace Reservo.Api.Controllers;

/// <summary>Профиль текущего пользователя.</summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Кто я: профиль пользователя, чей токен передан в запросе.</summary>
    /// <response code="200">Профиль.</response>
    /// <response code="401">Нет токена или он недействителен (<c>unauthorized</c>).</response>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<UserDto> Me(CancellationToken cancellationToken) =>
        userService.GetCurrentAsync(cancellationToken);
}
