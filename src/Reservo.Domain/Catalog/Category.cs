namespace Reservo.Domain.Catalog;

/// <summary>Категория ресурсов: «Переговорные», «Лаборатории» и т. п.</summary>
public class Category
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    // Нужен EF Core при загрузке из базы.
    private Category()
    {
    }

    public Category(string code, string name, string? description) => Update(code, name, description);

    public int Id { get; private set; }

    /// <summary>Стабильный ключ латиницей (<c>meeting-room</c>): для иконок на фронтенде и фильтров в URL.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public void Update(string code, string name, string? description)
    {
        Code = CatalogText.NormalizeCode(code);
        Name = name.Trim();
        Description = CatalogText.NullIfBlank(description);
    }
}
