using FluentValidation;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Resources;

/// <summary>Данные ресурса для создания или полного изменения.</summary>
public sealed record ResourceRequest
{
    public const int MaxCapacity = 1000;
    public const int MinFloor = -5;
    public const int MaxFloor = 100;
    public const int MinBookingLimitMinutes = 15;
    public const int MaxBookingLimitMinutes = 24 * 60;
    public const int MaxFeatures = 30;

    /// <summary>Id категории.</summary>
    /// <example>1</example>
    public int CategoryId { get; init; }

    /// <summary>Id корпуса.</summary>
    /// <example>1</example>
    public int LocationId { get; init; }

    /// <summary>Название.</summary>
    /// <example>Переговорная «Таймыр»</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание, необязательно.</summary>
    /// <example>Тихая переговорная на шесть человек с окнами во двор.</example>
    public string? Description { get; init; }

    /// <summary>Этаж, необязательно.</summary>
    /// <example>3</example>
    public int? Floor { get; init; }

    /// <summary>Вместимость в людях. Для оборудования не указывается.</summary>
    /// <example>6</example>
    public int? Capacity { get; init; }

    /// <summary>Ссылка на фото (http или https), необязательно.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Нужно ли подтверждение администратора для каждой брони.</summary>
    /// <example>false</example>
    public bool RequiresApproval { get; init; }

    /// <summary>Начало рабочего дня, местное время площадки (ЧЧ:ММ:СС).</summary>
    /// <example>08:00:00</example>
    public TimeOnly? OpenTime { get; init; }

    /// <summary>Конец рабочего дня, местное время площадки (ЧЧ:ММ:СС).</summary>
    /// <example>21:00:00</example>
    public TimeOnly? CloseTime { get; init; }

    /// <summary>Максимальная длительность одной брони в минутах. Пусто — общий лимит из настроек.</summary>
    /// <example>180</example>
    public int? MaxDurationMinutes { get; init; }

    /// <summary>Id оснащения. Пустой список — оснащения нет.</summary>
    /// <example>[1, 2]</example>
    public int[] FeatureIds { get; init; } = [];

    /// <summary>Вызывается после валидации, поэтому время открытия и закрытия уже точно заполнено.</summary>
    public ResourceDetails ToDetails() => new(
        CategoryId,
        LocationId,
        Name,
        Description,
        Floor,
        Capacity,
        ImageUrl,
        RequiresApproval,
        OpenTime!.Value,
        CloseTime!.Value,
        MaxDurationMinutes);
}

public sealed class ResourceRequestValidator : AbstractValidator<ResourceRequest>
{
    public ResourceRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Укажите категорию.");
        RuleFor(x => x.LocationId).GreaterThan(0).WithMessage("Укажите корпус.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Укажите название.")
            .MaximumLength(Resource.NameMaxLength).WithMessage($"Название не длиннее {Resource.NameMaxLength} символов.");

        RuleFor(x => x.Description)
            .MaximumLength(Resource.DescriptionMaxLength)
            .WithMessage($"Описание не длиннее {Resource.DescriptionMaxLength} символов.");

        RuleFor(x => x.Floor)
            .InclusiveBetween(ResourceRequest.MinFloor, ResourceRequest.MaxFloor)
            .WithMessage($"Этаж должен быть от {ResourceRequest.MinFloor} до {ResourceRequest.MaxFloor}.");

        RuleFor(x => x.Capacity)
            .InclusiveBetween(1, ResourceRequest.MaxCapacity)
            .WithMessage($"Вместимость должна быть от 1 до {ResourceRequest.MaxCapacity} человек.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(Resource.ImageUrlMaxLength).WithMessage($"Ссылка не длиннее {Resource.ImageUrlMaxLength} символов.")
            .Must(BeHttpUrl).WithMessage("Ссылка на фото должна быть полным адресом, начинающимся с http:// или https://.")
            .When(x => !string.IsNullOrWhiteSpace(x.ImageUrl));

        RuleFor(x => x.OpenTime).NotNull().WithMessage("Укажите время открытия.");

        RuleFor(x => x.CloseTime).NotNull().WithMessage("Укажите время закрытия.");

        // Отдельным правилом: условие When относится ко всей цепочке, и иначе отключило бы проверку NotNull выше.
        RuleFor(x => x.CloseTime)
            .Must((request, close) => close > request.OpenTime)
            .WithMessage("Время закрытия должно быть позже времени открытия.")
            .When(x => x.OpenTime is not null && x.CloseTime is not null);

        RuleFor(x => x.MaxDurationMinutes)
            .InclusiveBetween(ResourceRequest.MinBookingLimitMinutes, ResourceRequest.MaxBookingLimitMinutes)
            .WithMessage($"Лимит длительности брони — от {ResourceRequest.MinBookingLimitMinutes} до {ResourceRequest.MaxBookingLimitMinutes} минут.");

        RuleFor(x => x.FeatureIds)
            .NotNull().WithMessage("Передайте список оснащения (можно пустой).")
            .Must(ids => ids.Length <= ResourceRequest.MaxFeatures)
            .WithMessage($"Не больше {ResourceRequest.MaxFeatures} видов оснащения.");

        RuleForEach(x => x.FeatureIds).GreaterThan(0).WithMessage("Id оснащения должен быть положительным.");
    }

    private static bool BeHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

/// <summary>Вывод ресурса из эксплуатации.</summary>
public sealed record DeactivateResourceRequest
{
    // 400, а не 500: к причине добавляется «Ресурс выведен из эксплуатации: », а поле причины у брони — 500 символов.
    public const int ReasonMaxLength = 400;

    /// <summary>Причина. Её увидят владельцы броней, которые отменятся из-за вывода ресурса.</summary>
    /// <example>Ремонт проектора до конца недели</example>
    public string Reason { get; init; } = string.Empty;
}

public sealed class DeactivateResourceRequestValidator : AbstractValidator<DeactivateResourceRequest>
{
    public DeactivateResourceRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Укажите причину.")
            .MaximumLength(DeactivateResourceRequest.ReasonMaxLength)
            .WithMessage($"Причина не длиннее {DeactivateResourceRequest.ReasonMaxLength} символов.");
    }
}
