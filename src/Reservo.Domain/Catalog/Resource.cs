namespace Reservo.Domain.Catalog;

/// <summary>Редактируемые поля ресурса — один набор и для создания, и для изменения.</summary>
public sealed record ResourceDetails(
    int CategoryId,
    int LocationId,
    string Name,
    string? Description,
    int? Floor,
    int? Capacity,
    string? ImageUrl,
    bool RequiresApproval,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    int? MaxDurationMinutes,
    bool AllowsOvernight = false,
    string? PickupInfo = null,
    SeatPlacement? Seat = null);

/// <summary>Положение места на схеме коворкинга. Ряды и колонки нумеруются с 1.</summary>
public sealed record SeatPlacement(int SpaceId, int Number, int Row, int Column);

/// <summary>То, что бронируют: помещение или единица оборудования. Каждый ресурс — один экземпляр.</summary>
public class Resource
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int ImageUrlMaxLength = 500;
    public const int PickupInfoMaxLength = 300;
    public const int DeactivationReasonMaxLength = 400;

    // EF Core наполняет связь «многие-ко-многим» через это поле, а снаружи список доступен только для чтения.
    private readonly List<Feature> _features = [];

    // Нужен EF Core при загрузке из базы.
    private Resource()
    {
    }

    public Resource(ResourceDetails details, DateTimeOffset now)
    {
        Apply(details);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public int Id { get; private set; }

    public int CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public int LocationId { get; private set; }

    public Location Location { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public int? Floor { get; private set; }

    /// <summary>Вместимость в людях. У оборудования не задаётся.</summary>
    public int? Capacity { get; private set; }

    public string? ImageUrl { get; private set; }

    /// <summary>Брони этого ресурса подтверждает администратор.</summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>Начало рабочего дня, местное время площадки.</summary>
    public TimeOnly OpenTime { get; private set; }

    /// <summary>Конец рабочего дня, местное время площадки.</summary>
    public TimeOnly CloseTime { get; private set; }

    /// <summary>Максимальная длительность одной брони. Null — действует общий лимит из настроек.</summary>
    public int? MaxDurationMinutes { get; private set; }

    /// <summary>
    /// Бронь может длиться несколько дней (оборудование «на ночь», «на 3 дня»). Тогда рабочие часы — это часы выдачи
    /// и возврата: в них должны попадать начало и конец брони, а не вся бронь целиком (BR-06).
    /// </summary>
    public bool AllowsOvernight { get; private set; }

    /// <summary>Где и когда выдают и принимают оборудование, например «Стойка информации, 1 этаж».</summary>
    public string? PickupInfo { get; private set; }

    /// <summary>Коворкинг, если ресурс — место в нём.</summary>
    public int? SpaceId { get; private set; }

    public Space? Space { get; private set; }

    /// <summary>Номер места в коворкинге — его видят люди («Место 7»).</summary>
    public int? SeatNumber { get; private set; }

    /// <summary>Ряд места на схеме, с 1.</summary>
    public int? SeatRow { get; private set; }

    /// <summary>Колонка места на схеме, с 1.</summary>
    public int? SeatColumn { get; private set; }

    /// <summary>False — ресурс выведен из эксплуатации: он не удаляется, но бронировать его нельзя.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Почему ресурс выведен из эксплуатации. Показывается на карточке ресурса, пока он недоступен.</summary>
    public string? DeactivationReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<Feature> Features => _features;

    public void Update(ResourceDetails details, DateTimeOffset now)
    {
        Apply(details);
        UpdatedAt = now;
    }

    /// <summary>Заменяет оснащение ресурса целиком. Повторы отбрасываются.</summary>
    /// <remarks>
    /// Повторы ищутся по ссылке на объект, а не по Id: у ещё не сохранённых объектов Id у всех равен 0.
    /// Для загруженных из базы это то же самое — EF Core выдаёт один объект на одну запись.
    /// </remarks>
    public void SetFeatures(IEnumerable<Feature> features)
    {
        _features.Clear();
        _features.AddRange(features.Distinct());
    }

    public void Deactivate(string reason, DateTimeOffset now)
    {
        IsActive = false;
        DeactivationReason = reason.Trim();
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        DeactivationReason = null;
        UpdatedAt = now;
    }

    private void Apply(ResourceDetails details)
    {
        CategoryId = details.CategoryId;
        LocationId = details.LocationId;
        Name = details.Name.Trim();
        Description = CatalogText.NullIfBlank(details.Description);
        Floor = details.Floor;
        Capacity = details.Capacity;
        ImageUrl = CatalogText.NullIfBlank(details.ImageUrl);
        RequiresApproval = details.RequiresApproval;
        OpenTime = details.OpenTime;
        CloseTime = details.CloseTime;
        MaxDurationMinutes = details.MaxDurationMinutes;
        AllowsOvernight = details.AllowsOvernight;
        PickupInfo = CatalogText.NullIfBlank(details.PickupInfo);
        SpaceId = details.Seat?.SpaceId;
        SeatNumber = details.Seat?.Number;
        SeatRow = details.Seat?.Row;
        SeatColumn = details.Seat?.Column;
    }
}
