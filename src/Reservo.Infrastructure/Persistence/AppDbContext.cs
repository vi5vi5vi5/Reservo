using Microsoft.EntityFrameworkCore;
using Reservo.Application.Common;
using Reservo.Domain.Audit;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;
using Reservo.Domain.Notifications;
using Reservo.Domain.Users;

namespace Reservo.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Feature> Features => Set<Feature>();

    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Notification> Notifications => Set<Notification>();

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

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        // При включённом повторе временных сбоев (EnableRetryOnFailure) свою транзакцию нужно запускать через стратегию:
        // при сбое она повторит всю операцию целиком в новой транзакции.
        var strategy = Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            // Каждая попытка — с чистого листа: объекты, оставшиеся от неудачной попытки, не должны попасть в новую.
            ChangeTracker.Clear();

            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        });
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
