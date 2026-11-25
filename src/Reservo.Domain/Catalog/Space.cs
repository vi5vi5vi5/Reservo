namespace Reservo.Domain.Catalog;

/// <summary>Редактируемые поля коворкинга.</summary>
public sealed record SpaceDetails(
    int LocationId,
    string Name,
    string? Description,
    int? Floor,
    bool IsQuiet,
    int Rows,
    int Columns,
    int? AisleAfterColumn);

/// <summary>
/// Коворкинг — зал с отдельными местами. Сам он не бронируется: бронируют места, и каждое место — обычный
/// <see cref="Resource"/> со своим номером и положением на схеме. Поэтому на места действуют все правила
/// бронирования и защита от двойных броней без единой строки нового кода.
/// </summary>
public class Space
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 1000;
    public const int MaxRows = 30;
    public const int MaxColumns = 30;

    // Нужен EF Core при загрузке из базы.
    private Space()
    {
    }

    public Space(SpaceDetails details, DateTimeOffset now)
    {
        Apply(details);
        CreatedAt = now;
        UpdatedAt = now;
    }

    public int Id { get; private set; }

    public int LocationId { get; private set; }

    public Location Location { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public int? Floor { get; private set; }

    /// <summary>Тихая зона: без звонков и разговоров.</summary>
    public bool IsQuiet { get; private set; }

    /// <summary>Рядов на схеме зала.</summary>
    public int Rows { get; private set; }

    /// <summary>Мест в ряду на схеме зала.</summary>
    public int Columns { get; private set; }

    /// <summary>После какой колонки на схеме проход (нумерация с 1). Пусто — прохода нет.</summary>
    public int? AisleAfterColumn { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(SpaceDetails details, DateTimeOffset now)
    {
        Apply(details);
        UpdatedAt = now;
    }

    private void Apply(SpaceDetails details)
    {
        LocationId = details.LocationId;
        Name = details.Name.Trim();
        Description = CatalogText.NullIfBlank(details.Description);
        Floor = details.Floor;
        IsQuiet = details.IsQuiet;
        Rows = details.Rows;
        Columns = details.Columns;
        AisleAfterColumn = details.AisleAfterColumn;
    }
}
