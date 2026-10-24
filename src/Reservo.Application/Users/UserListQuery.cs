using FluentValidation;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Users;

namespace Reservo.Application.Users;

/// <summary>Фильтры списка пользователей.</summary>
public sealed class UserListQuery : PagedQuery
{
    public const int SearchMaxLength = 100;

    /// <summary>Поиск по email или имени, без учёта регистра.</summary>
    public string? Search { get; init; }

    /// <summary>Только пользователи с этой ролью.</summary>
    public UserRole? Role { get; init; }

    /// <summary><c>true</c> — только активные, <c>false</c> — только заблокированные.</summary>
    public bool? IsActive { get; init; }
}

public sealed class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        Include(new PagedQueryValidator());

        RuleFor(x => x.Search)
            .MaximumLength(UserListQuery.SearchMaxLength)
            .WithMessage($"Строка поиска не длиннее {UserListQuery.SearchMaxLength} символов.");
    }
}
