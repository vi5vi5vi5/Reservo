namespace Reservo.Domain.Catalog;

/// <summary>Оснащение ресурса: «Проектор», «Видеосвязь» и т. п.</summary>
public class Feature
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 100;

    // Нужен EF Core при загрузке из базы.
    private Feature()
    {
    }

    public Feature(string code, string name) => Update(code, name);

    public int Id { get; private set; }

    /// <summary>Стабильный ключ латиницей (<c>projector</c>): для иконок на фронтенде и фильтров в URL.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public void Update(string code, string name)
    {
        Code = CatalogText.NormalizeCode(code);
        Name = name.Trim();
    }
}
