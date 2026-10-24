using FluentValidation;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Features;

/// <summary>Вид оснащения.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Code">Стабильный ключ латиницей, например <c>projector</c>.</param>
/// <param name="Name">Название.</param>
public sealed record FeatureDto(int Id, string Code, string Name);

/// <summary>Данные оснащения для создания или изменения.</summary>
public sealed record FeatureRequest
{
    /// <summary>Стабильный ключ: латиница в нижнем регистре, цифры, дефисы.</summary>
    /// <example>document-camera</example>
    public string Code { get; init; } = string.Empty;

    /// <summary>Название.</summary>
    /// <example>Документ-камера</example>
    public string Name { get; init; } = string.Empty;
}

public sealed class FeatureRequestValidator : AbstractValidator<FeatureRequest>
{
    public FeatureRequestValidator()
    {
        RuleFor(x => x.Code).MustBeCatalogCode(Feature.CodeMaxLength);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Укажите название.")
            .MaximumLength(Feature.NameMaxLength).WithMessage($"Название не длиннее {Feature.NameMaxLength} символов.");
    }
}

public static class FeatureMappings
{
    public static FeatureDto ToDto(this Feature feature) =>
        new(feature.Id, feature.Code, feature.Name);
}
