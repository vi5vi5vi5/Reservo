using Microsoft.OpenApi;
using Reservo.Api.ErrorHandling;
using Reservo.Infrastructure;
using Reservo.Infrastructure.Persistence;
using Serilog;

// A minimal console logger that works even before configuration is loaded,
// so startup failures (e.g. database unreachable) still get logged.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // The real logger: sinks and levels come from the "Serilog" section of appsettings.
    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddReservoProblemDetails();

    builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Reservo API",
        Version = "v1",
        Description = "Бронирование аудиторий, лабораторий, переговорных и оборудования.",
    }));

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    var app = builder.Build();

    await app.Services.MigrateDatabaseAsync();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    // Swagger stays on in every environment: it is the demo tool for the project defence.
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "Reservo API");

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown on purpose by `dotnet ef` tooling and is not a real failure.
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
