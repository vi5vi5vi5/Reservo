using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Catalog.Locations;

namespace Reservo.Api.Controllers;

/// <summary>Справочник корпусов. Читать может кто угодно, изменять — только администратор.</summary>
[ApiController]
[Route("api/locations")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class LocationsController(ILocationService locationService) : ControllerBase
{
    /// <summary>Все корпуса по алфавиту.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<LocationDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<LocationDto>> GetAll(CancellationToken cancellationToken) =>
        locationService.GetAllAsync(cancellationToken);

    /// <summary>Корпус по id.</summary>
    /// <response code="404">Корпус не найден (<c>not_found</c>).</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<LocationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<LocationDto> GetById(int id, CancellationToken cancellationToken) =>
        locationService.GetByIdAsync(id, cancellationToken);

    /// <summary>Создать корпус.</summary>
    /// <response code="201">Корпус создан.</response>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>).</response>
    /// <response code="409">Корпус с таким названием уже есть (<c>catalog.duplicate</c>).</response>
    [HttpPost]
    [ProducesResponseType<LocationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LocationDto>> Create(LocationRequest request, CancellationToken cancellationToken)
    {
        var location = await locationService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = location.Id }, location);
    }

    /// <summary>Изменить корпус.</summary>
    /// <response code="404">Корпус не найден (<c>not_found</c>).</response>
    /// <response code="409">Корпус с таким названием уже есть (<c>catalog.duplicate</c>).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<LocationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<LocationDto> Update(int id, LocationRequest request, CancellationToken cancellationToken) =>
        locationService.UpdateAsync(id, request, cancellationToken);

    /// <summary>Удалить корпус. Можно только пустой — без ресурсов.</summary>
    /// <response code="204">Корпус удалён.</response>
    /// <response code="404">Корпус не найден (<c>not_found</c>).</response>
    /// <response code="409">В корпусе есть ресурсы (<c>in_use</c>).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await locationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
