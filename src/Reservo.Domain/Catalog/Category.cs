namespace Reservo.Domain.Catalog;

/// <summary>Категория ресурсов: «Переговорные», «Лаборатории», «Ноутбуки» и т. п.</summary>
public class Category
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    // Нужен EF Core при загрузке из базы.
    private Category()
    {
    }

    public Category(string code, string name, string? description, ResourceKind kind) => Update(code, name, description, kind);

    public int Id { get; private set; }

    /// <summary>Стабильный ключ латиницей (<c>meeting-room</c>): для иконок на фронтенде и фильтров в URL.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>Вид ресурсов категории: помещения, места в коворкинге или оборудование.</summary>
    public ResourceKind Kind { get; private set; }

    public void Update(string code, string name, string? description, ResourceKind kind)
    {
        Code = CatalogText.NormalizeCode(code);
        Name = name.Trim();
        Description = CatalogText.NullIfBlank(description);
        Kind = kind;
    }
}
