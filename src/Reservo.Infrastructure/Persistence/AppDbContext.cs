using Microsoft.EntityFrameworkCore;
using Reservo.Application.Common;
using Reservo.Domain.Catalog;
using Reservo.Domain.Users;

namespace Reservo.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Feature> Features => Set<Feature>();

    public DbSet<Resource> Resources => Set<Resource>();

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
        // btree_gist позволяет одному GiST-индексу сочетать «resource_id =» и «интервалы пересекаются».
        // На нём держится exclusion constraint против двойных броней (PROJECT.md, 5.3).
        modelBuilder.HasPostgresExtension("btree_gist");

        // Подхватывает все классы IEntityTypeConfiguration<T> из этой сборки.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
