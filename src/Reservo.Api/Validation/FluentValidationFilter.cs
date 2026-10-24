using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Reservo.Api.Validation;

/// <summary>
/// Перед выполнением действия контроллера запускает валидатор FluentValidation (если он есть) для каждого аргумента.
/// Некорректные данные не доходят ни до контроллеров, ни до сервисов: клиент получает 400 со списком проблем
/// по каждому полю — в том же формате, что и встроенная валидация ASP.NET Core.
/// </summary>
internal sealed class FluentValidationFilter(ProblemDetailsFactory problemDetailsFactory) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Отдельный словарь, а не context.ModelState: привязка модели уже завела там ключи вроде "Page",
        // и поиск без учёта регистра сохранил бы это написание вместо "page".
        var errors = new ModelStateDictionary();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
            {
                // Имена полей в camelCase — ровно как в JSON или строке запроса, которые прислал клиент.
                errors.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName), failure.ErrorMessage);
            }
        }

        if (errors.ErrorCount > 0)
        {
            var problem = problemDetailsFactory.CreateValidationProblemDetails(context.HttpContext, errors);
            context.Result = new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            return;
        }

        await next();
    }
}
