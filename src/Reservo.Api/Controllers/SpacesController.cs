using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Catalog.Spaces;

namespace Reservo.Api.Controllers;

/// <summary>Коворкинги и схемы мест. Смотреть может кто угодно, управлять — только администратор.</summary>
/// <remarks>
/// Место бронируется как обычный ресурс: <c>POST /api/bookings</c> с <c>resourceId</c> места. Сами места создаются через
/// <c>POST /api/resources</c> в категории вида <c>Seat</c> с указанием коворкинга, номера, ряда и колонки.
/// </remarks>
[ApiController]
[Route("api/spaces")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class SpacesController(ISpaceService spaceService) : ControllerBase
{
    /// <summary>Все коворкинги со схемами мест.</summary>
    /// <remarks>
    /// С интервалом <c>from</c>–<c>to</c> (не длиннее суток) у каждого места есть состояние (<c>Free</c>, <c>Busy</c>, <c>Mine</c>,
    /// <c>Unavailable</c>) и счётчик свободных мест. Без интервала приходит только схема.
    /// </remarks>
    /// <response code="400">Неверный интервал (<c>validation_failed</c>).</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<SpaceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<IReadOnlyList<SpaceDto>> GetAll([FromQuery] SpaceAvailabilityQuery query, CancellationToken cancellationToken) =>
        spaceService.GetAllAsync(query, cancellationToken);

    /// <summary>Коворкинг со схемой мест; с интервалом — с состоянием каждого места.</summary>
    /// <response code="404">Коворкинг не найден (<c>not_found</c>).</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<SpaceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<SpaceDto> GetById(int id, [FromQuery] SpaceAvailabilityQuery query, CancellationToken cancellationToken) =>
        spaceService.GetByIdAsync(id, query, cancellationToken);

    /// <summary>Создать коворкинг (пока без мест).</summary>
    /// <response code="201">Коворкинг создан.</response>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>, <c>reference_not_found</c>).</response>
    [HttpPost]
    [ProducesResponseType<SpaceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SpaceDto>> Create(SpaceRequest request, CancellationToken cancellationToken)
    {
        var space = await spaceService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = space.Id }, space);
    }

    /// <summary>Изменить коворкинг. Схему нельзя уменьшить так, чтобы места оказались за её краем.</summary>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>, <c>reference_not_found</c>).</response>
    /// <response code="404">Коворкинг не найден (<c>not_found</c>).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<SpaceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<SpaceDto> Update(int id, SpaceRequest request, CancellationToken cancellationToken) =>
        spaceService.UpdateAsync(id, request, cancellationToken);

    /// <summary>Удалить коворкинг. Можно только пустой — без мест.</summary>
    /// <response code="204">Коворкинг удалён.</response>
    /// <response code="404">Коворкинг не найден (<c>not_found</c>).</response>
    /// <response code="409">В коворкинге есть места (<c>in_use</c>).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await spaceService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
