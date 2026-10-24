using Microsoft.EntityFrameworkCore;
using Reservo.Domain.Catalog;
using Reservo.Domain.Users;

namespace Reservo.Application.Common;

/// <summary>
/// База данных глазами сервисов приложения. Реализуется классом AppDbContext в Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<Category> Categories { get; }

    DbSet<Location> Locations { get; }

    DbSet<Feature> Features { get; }

    DbSet<Resource> Resources { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
