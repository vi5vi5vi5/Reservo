using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reservo.Application.Auth;
using Reservo.Domain.Users;

namespace Reservo.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations, then seeds demo data. Runs on every startup and is idempotent.
    /// Migrating on startup is a deliberate simplification for a study project;
    /// production systems usually run migrations as a separate deploy step.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        await SeedAdminAsync(provider, db, cancellationToken);
    }

    private static async Task SeedAdminAsync(IServiceProvider provider, AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
        {
            return;
        }

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var options = provider.GetRequiredService<IOptions<SeedOptions>>().Value.Admin;

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning("No administrator exists and Seed:Admin:Password is not set, so none was created");
            return;
        }

        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var timeProvider = provider.GetRequiredService<TimeProvider>();

        db.Users.Add(new User(options.Email, options.FullName, hasher.Hash(options.Password), UserRole.Admin, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded administrator {Email}", options.Email);
    }
}
