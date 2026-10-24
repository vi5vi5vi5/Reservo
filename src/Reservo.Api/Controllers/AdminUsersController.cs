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

    /// <summary>Назначить роль: <c>User</c> или <c>Admin</c>.</summary>
    /// <remarks>Новая роль попадёт в access-токен пользователя при следующем входе или обмене refresh-токена.</remarks>
    /// <response code="404">Пользователь не найден (<c>not_found</c>).</response>
    /// <response code="409">Нельзя менять роль самому себе (<c>user.cannot_change_self</c>).</response>
    [HttpPut("{id:int}/role")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<UserDto> ChangeRole(int id, ChangeRoleRequest request, CancellationToken cancellationToken) =>
        userService.ChangeRoleAsync(id, request, cancellationToken);

    /// <summary>Заблокировать пользователя.</summary>
    /// <remarks>Войти и продлить сессию он больше не сможет: все его refresh-токены отзываются (BR-16).</remarks>
    /// <response code="404">Пользователь не найден (<c>not_found</c>).</response>
    /// <response code="409">Нельзя заблокировать себя (<c>user.cannot_change_self</c>).</response>
    [HttpPost("{id:int}/block")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<UserDto> Block(int id, CancellationToken cancellationToken) =>
        userService.BlockAsync(id, cancellationToken);

    /// <summary>Разблокировать пользователя.</summary>
    /// <response code="404">Пользователь не найден (<c>not_found</c>).</response>
    [HttpPost("{id:int}/unblock")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<UserDto> Unblock(int id, CancellationToken cancellationToken) =>
        userService.UnblockAsync(id, cancellationToken);
}
