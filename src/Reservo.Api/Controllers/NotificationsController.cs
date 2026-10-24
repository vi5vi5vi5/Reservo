using Microsoft.AspNetCore.Mvc;
using Reservo.Application.Common.Paging;
using Reservo.Application.Notifications;

namespace Reservo.Api.Controllers;

/// <summary>Уведомления текущего пользователя.</summary>
/// <remarks>Новые уведомления приходят и в реальном времени — событием <c>notificationReceived</c> хаба SignalR <c>/hubs/live</c>.</remarks>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    /// <summary>Мои уведомления, свежие сверху.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<NotificationDto>> Get([FromQuery] NotificationListQuery query, CancellationToken cancellationToken) =>
        notificationService.GetAsync(query, cancellationToken);

    /// <summary>Сколько непрочитанных — для счётчика на колокольчике.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public Task<UnreadCountDto> GetUnreadCount(CancellationToken cancellationToken) =>
        notificationService.GetUnreadCountAsync(cancellationToken);

    /// <summary>Отметить уведомление прочитанным.</summary>
    /// <response code="204">Отмечено.</response>
    /// <response code="404">Уведомления нет или оно чужое (<c>not_found</c>).</response>
    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        await notificationService.MarkReadAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Отметить все уведомления прочитанными.</summary>
    /// <response code="204">Отмечено.</response>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notificationService.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}
