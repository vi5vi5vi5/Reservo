using FluentValidation;

namespace Reservo.Application.Common.Paging;

/// <summary>Shared paging rules; list query validators include it.</summary>
public sealed class PagedQueryValidator : AbstractValidator<PagedQuery>
{
    public PagedQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Номер страницы начинается с 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PagedQuery.MaxPageSize)
            .WithMessage($"Размер страницы должен быть от 1 до {PagedQuery.MaxPageSize}.");
    }
}
