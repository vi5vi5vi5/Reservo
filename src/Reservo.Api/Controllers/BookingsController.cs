using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Reservo.Application.Bookings;
using Reservo.Application.Common.Paging;

namespace Reservo.Api.Controllers;

/// <summary>Бронирования текущего пользователя. Нужен вход.</summary>
/// <remarks>Время передаётся в ISO 8601 обязательно со смещением, например <c>2026-10-26T14:00:00+03:00</c>, а в ответах приходит в UTC.</remarks>
[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public sealed class BookingsController(IBookingService bookingService) : ControllerBase
{
    /// <summary>Забронировать ресурс.</summary>
    /// <remarks>
    /// Если ресурс требует согласования, бронь создаётся в статусе <c>Pending</c>, иначе сразу <c>Confirmed</c>.
    /// Проверяются все правила BR-02–BR-11: сетка 15 минут, будущее время, рабочие часы, вместимость, пересечения и лимит броней.
    /// </remarks>
    /// <response code="201">Бронь создана.</response>
    /// <response code="400">Нарушено правило бронирования (<c>booking.*</c>, <c>resource.inactive</c>) или данные не прошли проверку.</response>
    /// <response code="409">Время занято (<c>booking.time_conflict</c>) или у вас уже есть бронь на это время (<c>booking.user_overlap</c>).</response>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await bookingService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    /// <summary>Мои брони с фильтром по статусу и времени.</summary>
    /// <remarks>Предстоящие — <c>from</c> = текущий момент; прошедшие — <c>to</c> = текущий момент и <c>sortDir=Desc</c>.</remarks>
    [HttpGet("my")]
    [ProducesResponseType<PagedResult<BookingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<BookingDto>> GetMy([FromQuery] BookingListQuery query, CancellationToken cancellationToken) =>
        bookingService.GetMyAsync(query, cancellationToken);

    /// <summary>Бронь по id. Доступна владельцу и администратору.</summary>
    /// <response code="404">Брони нет или она чужая (<c>not_found</c>).</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<BookingDto> GetById(int id, CancellationToken cancellationToken) =>
        bookingService.GetByIdAsync(id, cancellationToken);

    /// <summary>Изменить свою бронь: время, тему, число участников.</summary>
    /// <remarks>
    /// Только до начала брони. Все правила проверяются заново. Если у ресурса с согласованием перенесли время
    /// подтверждённой брони, она снова уходит на согласование (BR-14).
    /// </remarks>
    /// <response code="403">Бронь чужая, а вы администратор: менять её может только владелец (<c>forbidden</c>).</response>
    /// <response code="404">Брони нет или она чужая (<c>not_found</c>).</response>
    /// <response code="409">Бронь уже началась, не активна или новое время занято.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BookingDto> Update(int id, UpdateBookingRequest request, CancellationToken cancellationToken) =>
        bookingService.UpdateAsync(id, request, cancellationToken);

    /// <summary>Отменить бронь.</summary>
    /// <remarks>
    /// Владелец — до начала брони, причина необязательна. Администратор — любую активную бронь, пока она не закончилась;
    /// для чужой брони причина обязательна. Тело запроса можно не передавать.
    /// </remarks>
    /// <response code="404">Брони нет или она чужая (<c>not_found</c>).</response>
    /// <response code="409">Бронь уже началась, закончилась или не активна.</response>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BookingDto> Cancel(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelBookingRequest? request,
        CancellationToken cancellationToken) =>
        bookingService.CancelAsync(id, request ?? new CancelBookingRequest(), cancellationToken);
}
