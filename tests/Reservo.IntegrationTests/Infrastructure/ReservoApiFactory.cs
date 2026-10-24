using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

// Одна фабрика на все тесты сборки: контейнер с базой и приложение поднимаются один раз.
[assembly: AssemblyFixture(typeof(Reservo.IntegrationTests.Infrastructure.ReservoApiFactory))]

namespace Reservo.IntegrationTests.Infrastructure;

/// <summary>
/// Поднимает API целиком — с настоящими контроллерами, валидацией, авторизацией, EF Core и миграциями —
/// в памяти теста, а базу данных — одноразовым PostgreSQL в Docker. Тесты общаются с API по HTTP,
/// ровно как настоящий клиент. После тестов контейнер удаляется.
/// </summary>
public sealed class ReservoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@reservo.example";
    public const string AdminPassword = "Admin-Test-123";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        // Запускаем приложение сразу, один раз, до всех тестов. Иначе его запустил бы первый вызов CreateClient(),
        // а при параллельных тестах таких «первых» вызовов несколько одновременно — и приложение стартовало бы дважды.
        _ = Server;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Окружение Testing: appsettings.Development.json не подключается, всё нужное задаётся здесь.
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Seed:Admin:Password", AdminPassword);

        // Демо-брони не создаём: каждый тест готовит свои данные сам.
        builder.UseSetting("Seed:DemoUserPassword", string.Empty);

        // Фоновая задача выключена: её логику тесты запускают сами, с поддельными часами.
        builder.UseSetting("Booking:LifecycleIntervalSeconds", "0");

        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
