namespace Reservo.Domain.Catalog;

/// <summary>
/// Вид ресурсов категории. От него зависят правила и показ: места в коворкинге не попадают в общий каталог,
/// а оборудование не мешает своему владельцу бронировать что-то ещё на то же время (BR-11).
/// </summary>
public enum ResourceKind
{
    /// <summary>Помещение: переговорная, аудитория, лаборатория.</summary>
    Room,

    /// <summary>Место в коворкинге. Такие ресурсы стоят на схеме зала (<see cref="Space"/>).</summary>
    Seat,

    /// <summary>Оборудование, которое берут с собой: проектор, ноутбук, камера, 3D-принтер.</summary>
    Equipment,
}

public static class ResourceKindExtensions
{
    /// <summary>
    /// Занимает ли бронь самого человека (BR-11): нельзя одновременно сидеть в двух переговорных,
    /// но взять ноутбук на три дня и при этом забронировать аудиторию — можно.
    /// </summary>
    public static bool OccupiesPerson(this ResourceKind kind) => kind != ResourceKind.Equipment;
}
