using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Bookings;
using Reservo.Application.Common.Paging;

namespace Reservo.Api.Controllers;

/// <summary>Администрирование: все брони и согласование заявок.</summary>
[ApiController]
[Route("api/admin/bookings")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class AdminBookingsController(IBookingService bookingService) : ControllerBase
{
    /// <summary>Все брони с фильтрами.</summary>
    /// <remarks>Очередь заявок на согласование: <c>status=Pending</c>. Отменить любую бронь администратор может через <c>POST /api/bookings/{id}/cancel</c>.</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<BookingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<BookingDto>> GetAll([FromQuery] AdminBookingListQuery query, CancellationToken cancellationToken) =>
        bookingService.GetAllAsync(query, cancellationToken);

    /// <summary>Одобрить заявку (<c>Pending</c> → <c>Confirmed</c>).</summary>
    /// <response code="404">Бронь не найдена (<c>not_found</c>).</response>
    /// <response code="409">Бронь не на согласовании или уже началась.</response>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BookingDto> Approve(int id, CancellationToken cancellationToken) =>
        bookingService.ApproveAsync(id, cancellationToken);

    /// <summary>Отклонить заявку с причиной (<c>Pending</c> → <c>Rejected</c>).</summary>
    /// <response code="404">Бронь не найдена (<c>not_found</c>).</response>
    /// <response code="409">Бронь не на согласовании.</response>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BookingDto> Reject(int id, RejectBookingRequest request, CancellationToken cancellationToken) =>
        bookingService.RejectAsync(id, request, cancellationToken);
}
