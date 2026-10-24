using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken cancellationToken);

    Task<CategoryDto> UpdateAsync(int id, CategoryRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}

/// <remarks>
/// Дубли кода и названия здесь не проверяются запросом заранее: их ловит уникальный индекс в базе,
/// а ConstraintErrors превращает нарушение в 409 <c>catalog.duplicate</c>. Удаление используемой
/// категории так же останавливает внешний ключ (Restrict) — получается 409 <c>in_use</c>.
/// </remarks>
public sealed class CategoryService(IAppDbContext db, ILogger<CategoryService> logger) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Code, c.Name, c.Description))
            .ToListAsync(cancellationToken);

    public async Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        (await FindAsync(id, cancellationToken)).ToDto();

    public async Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = new Category(request.Code, request.Name, request.Description);

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Создана категория {CategoryId} «{Name}»", category.Id, category.Name);
        return category.ToDto();
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);

        category.Update(request.Code, request.Name, request.Description);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Изменена категория {CategoryId}", id);
        return category.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Удалена категория {CategoryId}", id);
    }

    private async Task<Category> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Categories.FindAsync([id], cancellationToken)
        ?? throw CommonErrors.NotFound("Категория не найдена.");
}
