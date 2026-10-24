using FluentValidation;

namespace Reservo.Application.Catalog;

/// <summary>Правила проверки, общие для нескольких справочников.</summary>
internal static class CatalogValidationRules
{
    /// <summary>Код — латиница, цифры и дефисы между ними: <c>meeting-room</c>, <c>3d-printer</c>.</summary>
    public static IRuleBuilderOptions<T, string> MustBeCatalogCode<T>(this IRuleBuilder<T, string> rule, int maxLength) => rule
        .NotEmpty().WithMessage("Укажите код.")
        .MaximumLength(maxLength).WithMessage($"Код не длиннее {maxLength} символов.")
        .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
        .WithMessage("Код — латиница в нижнем регистре, цифры и дефисы между ними, например meeting-room.");
}
