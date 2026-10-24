using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Reservo.Api.Validation;

/// <summary>
/// Ошибки, которые находит сам ASP.NET Core ещё до наших валидаторов: число вместо текста, неизвестное значение
/// перечисления, сломанный JSON. Приводим их к тому же виду, что и ошибки FluentValidation:
/// русский текст и имена полей в camelCase.
/// </summary>
internal static class ModelBindingSetup
{
    public static void UseRussianModelBindingMessages(this MvcOptions options)
    {
        var messages = options.ModelBindingMessageProvider;

        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"Значение «{value}» не подходит для поля {Camel(field)}.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Значение «{value}» не подходит.");
        messages.SetUnknownValueIsInvalidAccessor(field => $"Недопустимое значение поля {Camel(field)}.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Недопустимое значение.");
        messages.SetValueIsInvalidAccessor(value => $"Значение «{value}» недопустимо.");
        messages.SetValueMustBeANumberAccessor(field => $"Поле {Camel(field)} должно быть числом.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "Значение должно быть числом.");
        messages.SetMissingBindRequiredValueAccessor(field => $"Не передано значение поля {Camel(field)}.");
        messages.SetMissingKeyOrValueAccessor(() => "Не передано значение.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "Тело запроса пустое, а должно содержать JSON.");
        messages.SetValueMustNotBeNullAccessor(value => $"Значение «{value}» недопустимо: поле не может быть пустым.");
    }

    /// <summary>Ответ 400 на ошибки привязки модели — в том же формате, что и у FluentValidationFilter.</summary>
    public static IActionResult CreateInvalidModelStateResponse(ActionContext context)
    {
        var errors = new ModelStateDictionary();

        foreach (var (key, entry) in context.ModelState)
        {
            foreach (var error in entry.Errors)
            {
                // Пустое сообщение бывает, когда JSON не разобрался: подробности парсера (типы .NET) клиенту не нужны.
                var message = string.IsNullOrEmpty(error.ErrorMessage) ? "Значение недопустимо или JSON некорректен." : error.ErrorMessage;
                errors.AddModelError(FieldName(key), message);
            }
        }

        var problemDetailsFactory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = problemDetailsFactory.CreateValidationProblemDetails(context.HttpContext, errors);

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    /// <summary>"SortBy" → "sortBy", "$.capacity" (путь внутри JSON) → "capacity".</summary>
    private static string FieldName(string key) =>
        key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : Camel(key);

    private static string Camel(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);
}
