using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Reservo.Api.Validation;

/// <summary>
/// Runs the FluentValidation validator (if one exists) for every action argument before the action executes.
/// Invalid input never reaches controllers or services: the client gets 400 with the list of problems per field,
/// in the same format as ASP.NET Core's built-in model validation.
/// </summary>
internal sealed class FluentValidationFilter(ProblemDetailsFactory problemDetailsFactory) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // A fresh dictionary rather than context.ModelState: model binding has already registered keys
        // like "Page" there, and the case-insensitive lookup would keep that casing instead of "page".
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
                // Field names in camelCase, exactly as they appear in the JSON or query string the client sent.
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
