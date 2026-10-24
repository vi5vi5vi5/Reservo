using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Catalog.Features;

namespace Reservo.Api.Controllers;

/// <summary>Справочник оснащения. Читать может кто угодно, изменять — только администратор.</summary>
[ApiController]
[Route("api/features")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class FeaturesController(IFeatureService featureService) : ControllerBase
{
    /// <summary>Всё оснащение по алфавиту.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<FeatureDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<FeatureDto>> GetAll(CancellationToken cancellationToken) =>
        featureService.GetAllAsync(cancellationToken);

    /// <summary>Оснащение по id.</summary>
    /// <response code="404">Оснащение не найдено (<c>not_found</c>).</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<FeatureDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<FeatureDto> GetById(int id, CancellationToken cancellationToken) =>
        featureService.GetByIdAsync(id, cancellationToken);

    /// <summary>Создать вид оснащения.</summary>
    /// <response code="201">Оснащение создано.</response>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>).</response>
    /// <response code="409">Код или название уже заняты (<c>catalog.duplicate</c>).</response>
    [HttpPost]
    [ProducesResponseType<FeatureDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeatureDto>> Create(FeatureRequest request, CancellationToken cancellationToken)
    {
        var feature = await featureService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = feature.Id }, feature);
    }

    /// <summary>Изменить вид оснащения.</summary>
    /// <response code="404">Оснащение не найдено (<c>not_found</c>).</response>
    /// <response code="409">Код или название уже заняты (<c>catalog.duplicate</c>).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<FeatureDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<FeatureDto> Update(int id, FeatureRequest request, CancellationToken cancellationToken) =>
        featureService.UpdateAsync(id, request, cancellationToken);

    /// <summary>Удалить вид оснащения. Можно только тот, что не указан ни у одного ресурса.</summary>
    /// <response code="204">Оснащение удалено.</response>
    /// <response code="404">Оснащение не найдено (<c>not_found</c>).</response>
    /// <response code="409">Оснащение указано у ресурсов (<c>in_use</c>).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await featureService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
