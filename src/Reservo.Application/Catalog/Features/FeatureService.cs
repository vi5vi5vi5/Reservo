using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Features;

public interface IFeatureService
{
    Task<IReadOnlyList<FeatureDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<FeatureDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<FeatureDto> CreateAsync(FeatureRequest request, CancellationToken cancellationToken);

    Task<FeatureDto> UpdateAsync(int id, FeatureRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}

/// <remarks>Дубли и удаление используемого оснащения ловит база — см. комментарий к CategoryService.</remarks>
public sealed class FeatureService(IAppDbContext db, ILogger<FeatureService> logger) : IFeatureService
{
    public async Task<IReadOnlyList<FeatureDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Features
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new FeatureDto(f.Id, f.Code, f.Name))
            .ToListAsync(cancellationToken);

    public async Task<FeatureDto> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        (await FindAsync(id, cancellationToken)).ToDto();

    public async Task<FeatureDto> CreateAsync(FeatureRequest request, CancellationToken cancellationToken)
    {
        var feature = new Feature(request.Code, request.Name);

        db.Features.Add(feature);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Создано оснащение {FeatureId} «{Name}»", feature.Id, feature.Name);
        return feature.ToDto();
    }

    public async Task<FeatureDto> UpdateAsync(int id, FeatureRequest request, CancellationToken cancellationToken)
    {
        var feature = await FindAsync(id, cancellationToken);

        feature.Update(request.Code, request.Name);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Изменено оснащение {FeatureId}", id);
        return feature.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var feature = await FindAsync(id, cancellationToken);

        db.Features.Remove(feature);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Удалено оснащение {FeatureId}", id);
    }

    private async Task<Feature> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Features.FindAsync([id], cancellationToken)
        ?? throw CommonErrors.NotFound("Оснащение не найдено.");
}
