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
    int? MaxDurationMinutes);

/// <summary>То, что бронируют: помещение или единица оборудования. Каждый ресурс — один экземпляр.</summary>
public class Resource
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int ImageUrlMaxLength = 500;

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

    /// <summary>False — ресурс выведен из эксплуатации: он не удаляется, но бронировать его нельзя.</summary>
    public bool IsActive { get; private set; }

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

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
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
    }
}
