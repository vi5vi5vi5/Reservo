using Microsoft.AspNetCore.WebUtilities;

namespace Reservo.Api.ErrorHandling;

/// <summary>
/// Every error response is RFC 9457 ProblemDetails with a machine-readable "code" (PROJECT.md, 7.1).
/// </summary>
internal static class ProblemDetailsSetup
{
    public const string CodeKey = "code";

    public static IServiceCollection AddReservoProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;

            if (problem is HttpValidationProblemDetails)
            {
                problem.Title = "Проверьте введённые данные.";
            }
            else if (problem.Status is { } status
                     && problem.Title == ReasonPhrases.GetReasonPhrase(status)
                     && DefaultTitle(status) is { } russianTitle)
            {
                // Framework-generated responses ("Unauthorized", "Not Found") get a Russian title like our own errors.
                problem.Title = russianTitle;
            }

            // Responses without an explicit code (e.g. 404 for an unknown URL) get a default one by status.
            problem.Extensions.TryAdd(CodeKey, DefaultCode(problem.Status));
        });

        // Handlers run in registration order: business errors first, then the catch-all.
        services.AddExceptionHandler<AppExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    private static string DefaultCode(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => "validation_failed",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status409Conflict => "conflict",
        >= 500 or null => "internal_error",
        _ => $"http_{status}",
    };

    private static string? DefaultTitle(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Некорректный запрос.",
        StatusCodes.Status401Unauthorized => "Требуется вход в систему.",
        StatusCodes.Status403Forbidden => "Недостаточно прав.",
        StatusCodes.Status404NotFound => "Не найдено.",
        StatusCodes.Status405MethodNotAllowed => "Метод не поддерживается.",
        _ => null,
    };
}
