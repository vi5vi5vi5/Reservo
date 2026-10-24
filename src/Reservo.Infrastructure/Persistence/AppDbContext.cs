using Microsoft.EntityFrameworkCore;
using Reservo.Application.Common;
using Reservo.Domain.Users;

namespace Reservo.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ConstraintErrors.TryTranslate(ex, out var error))
        {
            throw error;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // btree_gist lets a GiST index combine "resource_id =" with "time ranges overlap".
        // The exclusion constraint against double bookings relies on it (PROJECT.md, 5.3).
        modelBuilder.HasPostgresExtension("btree_gist");

        // Picks up every IEntityTypeConfiguration<T> class in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
