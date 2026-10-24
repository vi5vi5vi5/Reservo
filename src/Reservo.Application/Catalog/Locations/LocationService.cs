using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Locations;

public interface ILocationService
{
    Task<IReadOnlyList<LocationDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<LocationDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<LocationDto> CreateAsync(LocationRequest request, CancellationToken cancellationToken);

    Task<LocationDto> UpdateAsync(int id, LocationRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}

/// <remarks>Дубли и удаление используемого корпуса ловит база — см. комментарий к CategoryService.</remarks>
public sealed class LocationService(IAppDbContext db, ILogger<LocationService> logger) : ILocationService
{
    public async Task<IReadOnlyList<LocationDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Locations
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .Select(l => new LocationDto(l.Id, l.Name, l.Address))
            .ToListAsync(cancellationToken);

    public async Task<LocationDto> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        (await FindAsync(id, cancellationToken)).ToDto();

    public async Task<LocationDto> CreateAsync(LocationRequest request, CancellationToken cancellationToken)
    {
        var location = new Location(request.Name, request.Address);

        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Создан корпус {LocationId} «{Name}»", location.Id, location.Name);
        return location.ToDto();
    }

    public async Task<LocationDto> UpdateAsync(int id, LocationRequest request, CancellationToken cancellationToken)
    {
        var location = await FindAsync(id, cancellationToken);

        location.Update(request.Name, request.Address);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Изменён корпус {LocationId}", id);
        return location.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var location = await FindAsync(id, cancellationToken);

        db.Locations.Remove(location);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Удалён корпус {LocationId}", id);
    }

    private async Task<Location> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Locations.FindAsync([id], cancellationToken)
        ?? throw CommonErrors.NotFound("Корпус не найден.");
}
