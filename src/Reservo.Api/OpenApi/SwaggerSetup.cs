using Microsoft.OpenApi;

namespace Reservo.Api.OpenApi;

internal static class SwaggerSetup
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddReservoSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Reservo API",
                Version = "v1",
                Description = "Бронирование аудиторий, лабораторий, переговорных и оборудования.",
            });

            // Описания эндпоинтов и полей DTO берутся из XML-комментариев /// в коде.
            foreach (var xmlFile in new[] { "Reservo.Api.xml", "Reservo.Application.xml" })
            {
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
            }

            // Кнопка "Authorize": токен вставляется один раз, и Swagger отправляет его с каждым запросом.
            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access-токен из ответа /api/auth/login или /api/auth/register.",
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
            });
        });

        return services;
    }
}
