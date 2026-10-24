using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Catalog.Resources;
using Reservo.Application.Common.Paging;

namespace Reservo.Api.Controllers;

/// <summary>Каталог ресурсов. Смотреть может кто угодно, управлять — только администратор.</summary>
[ApiController]
[Route("api/resources")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class ResourcesController(IResourceService resourceService) : ControllerBase
{
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
    /// <remarks>Ресурс не удаляется, но пропадает из каталога и его нельзя бронировать. Повторный вызов ничего не меняет.</remarks>
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
