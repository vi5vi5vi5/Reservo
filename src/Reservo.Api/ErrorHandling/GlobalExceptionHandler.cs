using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Reservo.Api.ErrorHandling;

/// <summary>
/// Последний рубеж: любое необработанное исключение превращается в ответ 500 в формате ProblemDetails.
/// Подробности уходят только в лог — клиент никогда не видит стектрейс.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Необработанное исключение при обработке {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Внутренняя ошибка сервера",
                Detail = "Что-то пошло не так. Подробности записаны в лог.",
                Extensions = { [ProblemDetailsSetup.CodeKey] = "internal_error" },
            },
        });
    }
}
