using Microsoft.AspNetCore.WebUtilities;

namespace Reservo.Api.ErrorHandling;

/// <summary>
/// Любой ответ с ошибкой — это ProblemDetails (RFC 9457) с машиночитаемым полем "code" (PROJECT.md, 7.1).
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
                // Ответы, которые сформировал сам фреймворк ("Unauthorized", "Not Found"), получают русский заголовок, как и наши ошибки.
                problem.Title = russianTitle;
            }

            // Ответы без явного кода (например, 404 на неизвестный адрес) получают код по умолчанию по статусу.
            problem.Extensions.TryAdd(CodeKey, DefaultCode(problem.Status));
        });

        // Обработчики вызываются в порядке регистрации: сначала бизнес-ошибки, затем «ловушка» для всего остального.
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
