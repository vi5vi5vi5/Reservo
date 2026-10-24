using Microsoft.EntityFrameworkCore;
using Reservo.Domain.Users;

namespace Reservo.Application.Common;

/// <summary>
/// The database as seen by application services. Implemented by AppDbContext in Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
