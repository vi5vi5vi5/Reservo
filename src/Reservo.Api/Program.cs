using System.Text.Json.Serialization;
using Reservo.Api.Auth;
using Reservo.Api.ErrorHandling;
using Reservo.Api.OpenApi;
using Reservo.Api.Validation;
using Reservo.Application;
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

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddReservoAuth();

    builder.Services
        .AddControllers(options =>
        {
            options.Filters.Add<FluentValidationFilter>();
            // Required fields are checked by FluentValidation (with Russian messages), not by MVC's implicit [Required].
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        })
        // Enums travel as text: "role": "Admin", not "role": 1.
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddReservoProblemDetails();
    builder.Services.AddReservoSwagger();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    var app = builder.Build();

    await app.Services.InitializeDatabaseAsync();

    // First in the pipeline, so it logs the final status code — after error handlers have turned exceptions into 4xx.
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Swagger stays on in every environment: it is the demo tool for the project defence.
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "Reservo API";
        options.EnablePersistAuthorization();
    });

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

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
