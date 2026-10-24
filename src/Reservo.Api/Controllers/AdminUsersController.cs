using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Common.Paging;
using Reservo.Application.Users;

namespace Reservo.Api.Controllers;

/// <summary>Администрирование: пользователи.</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class AdminUsersController(IUserService userService) : ControllerBase
{
    /// <summary>Список пользователей с поиском, фильтрами и пагинацией.</summary>
    /// <response code="200">Страница списка.</response>
    /// <response code="400">Неверные параметры (<c>validation_failed</c>).</response>
    /// <response code="401">Нет токена или он недействителен (<c>unauthorized</c>).</response>
    /// <response code="403">Нужна роль администратора (<c>forbidden</c>).</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<PagedResult<UserDto>> GetUsers([FromQuery] UserListQuery query, CancellationToken cancellationToken) =>
        userService.GetListAsync(query, cancellationToken);
}
