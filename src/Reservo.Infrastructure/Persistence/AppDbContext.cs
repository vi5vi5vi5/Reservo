using Microsoft.EntityFrameworkCore;

namespace Reservo.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // btree_gist lets a GiST index combine "resource_id =" with "time ranges overlap".
        // The exclusion constraint against double bookings relies on it (PROJECT.md, 5.3).
        modelBuilder.HasPostgresExtension("btree_gist");

        // Picks up every IEntityTypeConfiguration<T> class in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
