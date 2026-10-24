using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Bookings;
using Reservo.Application.Catalog.Resources;
using Reservo.Application.Common.Paging;

namespace Reservo.Api.Controllers;

/// <summary>Каталог ресурсов. Смотреть может кто угодно, управлять — только администратор.</summary>
[ApiController]
[Route("api/resources")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class ResourcesController(IResourceService resourceService, IAvailabilityService availabilityService) : ControllerBase
{
    /// <summary>Ресурсы, свободные на весь интервал <c>from</c>–<c>to</c>, с теми же фильтрами, что и каталог.</summary>
    /// <remarks>
    /// «Свободен» значит: нет активных броней, пересекающих интервал, интервал укладывается в лимит длительности брони ресурса
    /// и — если интервал в пределах одного дня — в рабочие часы ресурса. Время со смещением, например
    /// <c>from=2026-10-26T14:00:00+03:00</c> (в адресной строке «+» кодируется как <c>%2B</c>, Swagger делает это сам).
    /// </remarks>
    /// <response code="200">Страница свободных ресурсов.</response>
    /// <response code="400">Неверный интервал или фильтры (<c>validation_failed</c>).</response>
    [HttpGet("available")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ResourceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<ResourceDto>> GetAvailable([FromQuery] AvailableResourcesQuery query, CancellationToken cancellationToken) =>
        availabilityService.GetAvailableResourcesAsync(query, cancellationToken);

    /// <summary>Расписание ресурса: занятые интервалы за окно <c>from</c>–<c>to</c> (не больше 31 дня).</summary>
    /// <remarks>Тема, владелец и id брони видны только владельцу брони и администратору (BR-17), остальным — просто «занято».</remarks>
    /// <response code="400">Неверное окно (<c>validation_failed</c>).</response>
    /// <response code="404">Ресурс не найден (<c>not_found</c>).</response>
    [HttpGet("{id:int}/schedule")]
    [AllowAnonymous]
    [ProducesResponseType<ResourceScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ResourceScheduleDto> GetSchedule(int id, [FromQuery] ScheduleQuery query, CancellationToken cancellationToken) =>
        availabilityService.GetScheduleAsync(id, query, cancellationToken);

    /// <summary>Каталог: поиск, фильтры, сортировка и пагинация.</summary>
    /// <remarks>
    /// Фильтр по оснащению оставляет ресурсы, у которых есть всё перечисленное: <c>featureIds=1&amp;featureIds=4</c>.
    /// Выведенные из эксплуатации ресурсы показываются только администратору с <c>includeInactive=true</c>.
    /// </remarks>
    /// <response code="200">Страница каталога.</response>
    /// <response code="400">Неверные параметры (<c>validation_failed</c>).</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ResourceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<ResourceDto>> GetList([FromQuery] ResourceListQuery query, CancellationToken cancellationToken) =>
        resourceService.GetListAsync(query, cancellationToken);

    /// <summary>Карточка ресурса.</summary>
    /// <response code="404">Ресурс не найден (<c>not_found</c>).</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<ResourceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ResourceDto> GetById(int id, CancellationToken cancellationToken) =>
        resourceService.GetByIdAsync(id, cancellationToken);

    /// <summary>Создать ресурс.</summary>
    /// <response code="201">Ресурс создан.</response>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>) или указана несуществующая категория, корпус или оснащение (<c>reference_not_found</c>).</response>
    [HttpPost]
    [ProducesResponseType<ResourceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResourceDto>> Create(ResourceRequest request, CancellationToken cancellationToken)
    {
        var resource = await resourceService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = resource.Id }, resource);
    }

    /// <summary>Изменить ресурс целиком, включая список оснащения.</summary>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>, <c>reference_not_found</c>).</response>
    /// <response code="404">Ресурс не найден (<c>not_found</c>).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ResourceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ResourceDto> Update(int id, ResourceRequest request, CancellationToken cancellationToken) =>
        resourceService.UpdateAsync(id, request, cancellationToken);

    /// <summary>Вывести ресурс из эксплуатации.</summary>
    /// <remarks>
    /// Ресурс не удаляется, но пропадает из каталога и его нельзя бронировать. Все его активные брони,
    /// которые ещё не закончились, отменяются с указанной причиной (BR-15). Повторный вызов ничего не меняет.
    /// </remarks>
    /// <response code="204">Ресурс выведен из эксплуатации.</response>
    /// <response code="404">Ресурс не найден (<c>not_found</c>).</response>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, DeactivateResourceRequest request, CancellationToken cancellationToken)
    {
        await resourceService.DeactivateAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Вернуть ресурс в эксплуатацию.</summary>
    /// <response code="204">Ресурс снова доступен.</response>
    /// <response code="404">Ресурс не найден (<c>not_found</c>).</response>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        await resourceService.ActivateAsync(id, cancellationToken);
        return NoContent();
    }
}
