using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Catalog.Categories;

namespace Reservo.Api.Controllers;

/// <summary>Справочник категорий ресурсов. Читать может кто угодно, изменять — только администратор.</summary>
[ApiController]
[Route("api/categories")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    /// <summary>Все категории по алфавиту.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CategoryDto>> GetAll(CancellationToken cancellationToken) =>
        categoryService.GetAllAsync(cancellationToken);

    /// <summary>Категория по id.</summary>
    /// <response code="404">Категория не найдена (<c>not_found</c>).</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<CategoryDto> GetById(int id, CancellationToken cancellationToken) =>
        categoryService.GetByIdAsync(id, cancellationToken);

    /// <summary>Создать категорию.</summary>
    /// <response code="201">Категория создана.</response>
    /// <response code="400">Данные не прошли проверку (<c>validation_failed</c>).</response>
    /// <response code="409">Код или название уже заняты (<c>catalog.duplicate</c>).</response>
    [HttpPost]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    /// <summary>Изменить категорию.</summary>
    /// <response code="404">Категория не найдена (<c>not_found</c>).</response>
    /// <response code="409">Код или название уже заняты (<c>catalog.duplicate</c>).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<CategoryDto> Update(int id, CategoryRequest request, CancellationToken cancellationToken) =>
        categoryService.UpdateAsync(id, request, cancellationToken);

    /// <summary>Удалить категорию. Можно только пустую — без ресурсов.</summary>
    /// <response code="204">Категория удалена.</response>
    /// <response code="404">Категория не найдена (<c>not_found</c>).</response>
    /// <response code="409">В категории есть ресурсы (<c>in_use</c>).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await categoryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
