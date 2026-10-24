namespace Reservo.Domain.Catalog;

/// <summary>Корпус университета, в котором находятся ресурсы.</summary>
public class Location
{
    public const int NameMaxLength = 100;
    public const int AddressMaxLength = 300;

    // Нужен EF Core при загрузке из базы.
    private Location()
    {
    }

    public Location(string name, string? address) => Update(name, address);

    public int Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Address { get; private set; }

    public void Update(string name, string? address)
    {
        Name = name.Trim();
        Address = CatalogText.NullIfBlank(address);
    }
}
