using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Reservo.Application.Auth;
using Reservo.Application.Common;
using Reservo.Infrastructure.Auth;
using Reservo.Infrastructure.Bookings;
using Reservo.Infrastructure.Persistence;

namespace Reservo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Не задана строка подключения 'Default'.");

        services.AddDbContext<AppDbContext>(options => options
            // Повтор при временных сбоях: обрыв связи, а главное — взаимоблокировка (deadlock), которую PostgreSQL
            // устраивает, когда несколько транзакций одновременно вставляют пересекающиеся брони под exclusion constraint.
            // На повторе вставка упирается в ограничение или в проверку в коде, и клиент получает честный 409.
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(2),
                errorCodesToAdd: null))
            .UseSnakeCaseNamingConvention()
            // Нарушение ограничения (дубль, удаление используемой записи) — ожидаемая ситуация: ConstraintErrors
            // превращает её в ответ 409. Поэтому EF Core не должен писать о ней ERR со стектрейсом.
            // Неожиданные ошибки базы всё равно попадут в лог через GlobalExceptionHandler.
            .ConfigureWarnings(warnings => warnings.Log(
                (RelationalEventId.CommandError, LogLevel.Debug),
                (CoreEventId.SaveChangesFailed, LogLevel.Debug))));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        // ValidateOnStart: без JWT-ключа или со слишком коротким ключом приложение не запустится и сразу скажет почему,
        // а не упадёт позже, на первом входе пользователя.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= JwtOptions.MinKeyBytes,
                $"Jwt:Key должен быть не короче {JwtOptions.MinKeyBytes} байт.")
            .Validate(o => o.Issuer.Length > 0 && o.Audience.Length > 0, "Нужно задать Jwt:Issuer и Jwt:Audience.")
            .ValidateOnStart();

        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName));

        services.AddBookingPolicy(configuration);

        // Единственный источник «текущего времени». В тестах его можно заменить поддельными часами.
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}
