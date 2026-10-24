using System.Text.Json.Serialization;
using Reservo.Api.Auth;
using Reservo.Api.ErrorHandling;
using Reservo.Api.OpenApi;
using Reservo.Api.Validation;
using Reservo.Application;
using Reservo.Infrastructure;
using Reservo.Infrastructure.Persistence;
using Serilog;

// Минимальный логгер в консоль, который работает ещё до загрузки конфигурации:
// так в лог попадают даже ошибки запуска (например, недоступная база).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Основной логгер: куда писать и какие уровни — из секции "Serilog" в appsettings.
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
            // Обязательность полей проверяет FluentValidation (с русскими сообщениями), а не неявный [Required] из MVC.
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            options.UseRussianModelBindingMessages();
        })
        .AddJsonOptions(options =>
        {
            // Перечисления передаются текстом: "role": "Admin", а не "role": 1.
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            // Не показываем клиенту внутренние сообщения парсера JSON с именами типов .NET.
            options.AllowInputFormatterExceptionMessages = false;
        })
        .ConfigureApiBehaviorOptions(options =>
            options.InvalidModelStateResponseFactory = ModelBindingSetup.CreateInvalidModelStateResponse);

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

    // Первым в конвейере, чтобы логировать итоговый статус — уже после того,
    // как обработчики ошибок превратили исключения в ответы 4xx.
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Swagger включён в любом окружении: это инструмент демонстрации на защите.
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
    // HostAbortedException намеренно выбрасывает инструмент `dotnet ef`, это не настоящая ошибка.
    Log.Fatal(ex, "Приложение аварийно завершило работу");
}
finally
{
    await Log.CloseAndFlushAsync();
}
