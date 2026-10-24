using FluentValidation;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Locations;

/// <summary>Корпус.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Name">Название корпуса.</param>
/// <param name="Address">Адрес.</param>
public sealed record LocationDto(int Id, string Name, string? Address);

/// <summary>Данные корпуса для создания или изменения.</summary>
public sealed record LocationRequest
{
    /// <summary>Название корпуса.</summary>
    /// <example>Корпус В</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Адрес, необязательно.</summary>
    /// <example>ул. Университетская, 5</example>
    public string? Address { get; init; }
}

public sealed class LocationRequestValidator : AbstractValidator<LocationRequest>
{
    public LocationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Укажите название.")
            .MaximumLength(Location.NameMaxLength).WithMessage($"Название не длиннее {Location.NameMaxLength} символов.");

        RuleFor(x => x.Address)
            .MaximumLength(Location.AddressMaxLength).WithMessage($"Адрес не длиннее {Location.AddressMaxLength} символов.");
    }
}

public static class LocationMappings
{
    public static LocationDto ToDto(this Location location) =>
        new(location.Id, location.Name, location.Address);
}
