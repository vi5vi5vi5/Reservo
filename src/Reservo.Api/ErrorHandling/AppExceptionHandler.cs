using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Reservo.Application.Common.Errors;

namespace Reservo.Api.ErrorHandling;

/// <summary>
/// Превращает ожидаемые бизнес-ошибки (<see cref="AppException"/>) в ProblemDetails с нужным HTTP-статусом.
/// Всё остальное достаётся <see cref="GlobalExceptionHandler"/>.
/// </summary>
internal sealed class AppExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException error)
        {
            return false;
        }

        // Бизнес-ошибка — нормальное поведение, а не сбой приложения: уровень Information, а не Error.
        logger.LogInformation("Запрос отклонён: {Code} — {Message}", error.Code, error.Message);

        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = error.Message,
                Detail = error.Detail,
                Extensions = { [ProblemDetailsSetup.CodeKey] = error.Code },
            },
        });
    }
}
