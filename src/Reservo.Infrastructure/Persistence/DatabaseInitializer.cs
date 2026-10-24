using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reservo.Application.Auth;
using Reservo.Domain.Users;
using Reservo.Infrastructure.Persistence.Seed;

namespace Reservo.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Применяет недостающие миграции, затем добавляет демо-данные. Выполняется при каждом запуске,
    /// и повторный запуск ничего не дублирует. Миграции при старте — осознанное упрощение учебного проекта:
    /// в промышленных системах их обычно применяют отдельным шагом развёртывания.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var timeProvider = provider.GetRequiredService<TimeProvider>();

        await SeedAdminAsync(provider, db, timeProvider, logger, cancellationToken);
        await CatalogSeeder.SeedAsync(db, timeProvider, logger, cancellationToken);
    }

    private static async Task SeedAdminAsync(
        IServiceProvider provider, AppDbContext db, TimeProvider timeProvider, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
        {
            return;
        }

        var options = provider.GetRequiredService<IOptions<SeedOptions>>().Value.Admin;

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning("Администратора в базе нет, а Seed:Admin:Password не задан, поэтому администратор не создан");
            return;
        }

        var hasher = provider.GetRequiredService<IPasswordHasher>();

        db.Users.Add(new User(options.Email, options.FullName, hasher.Hash(options.Password), UserRole.Admin, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Создан администратор {Email}", options.Email);
    }
}
