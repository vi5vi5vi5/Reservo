using FluentValidation;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Categories;

/// <summary>Категория ресурсов.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Code">Стабильный ключ латиницей, например <c>meeting-room</c>.</param>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
public sealed record CategoryDto(int Id, string Code, string Name, string? Description);

/// <summary>Данные категории для создания или изменения.</summary>
public sealed record CategoryRequest
{
    /// <summary>Стабильный ключ: латиница в нижнем регистре, цифры, дефисы.</summary>
    /// <example>coworking</example>
    public string Code { get; init; } = string.Empty;

    /// <summary>Название.</summary>
    /// <example>Коворкинги</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание, необязательно.</summary>
    /// <example>Открытые пространства для самостоятельной работы</example>
    public string? Description { get; init; }
}

public sealed class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(x => x.Code).MustBeCatalogCode(Category.CodeMaxLength);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Укажите название.")
            .MaximumLength(Category.NameMaxLength).WithMessage($"Название не длиннее {Category.NameMaxLength} символов.");

        RuleFor(x => x.Description)
            .MaximumLength(Category.DescriptionMaxLength)
            .WithMessage($"Описание не длиннее {Category.DescriptionMaxLength} символов.");
    }
}

public static class CategoryMappings
{
    public static CategoryDto ToDto(this Category category) =>
        new(category.Id, category.Code, category.Name, category.Description);
}
