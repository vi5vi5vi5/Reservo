using Reservo.Domain.Catalog;

namespace Reservo.UnitTests.Catalog;

public class CatalogEntityTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(3);

    private static ResourceDetails Details(string name = "Переговорная «Байкал»") => new(
        CategoryId: 1,
        LocationId: 1,
        Name: name,
        Description: "  ",
        Floor: 2,
        Capacity: 8,
        ImageUrl: "",
        RequiresApproval: false,
        OpenTime: new TimeOnly(8, 0),
        CloseTime: new TimeOnly(21, 0),
        MaxDurationMinutes: null);

    [Fact]
    public void Category_code_is_trimmed_and_lowercased()
    {
        var category = new Category("  Meeting-Room ", " Переговорные ", null);

        Assert.Equal("meeting-room", category.Code);
        Assert.Equal("Переговорные", category.Name);
    }

    [Fact]
    public void Blank_optional_text_becomes_null()
    {
        var category = new Category("lab", "Лаборатории", "   ");
        var location = new Location("Технопарк", "");

        Assert.Null(category.Description);
        Assert.Null(location.Address);
    }

    [Fact]
    public void New_resource_is_active_and_cleans_text()
    {
        var resource = new Resource(Details("  Переговорная «Байкал»  "), Created);

        Assert.True(resource.IsActive);
        Assert.Equal("Переговорная «Байкал»", resource.Name);
        Assert.Null(resource.Description);
        Assert.Null(resource.ImageUrl);
        Assert.Equal(Created, resource.CreatedAt);
        Assert.Equal(Created, resource.UpdatedAt);
    }

    [Fact]
    public void Update_changes_fields_and_updated_at_but_not_created_at()
    {
        var resource = new Resource(Details(), Created);

        resource.Update(Details("Переговорная «Ладога»") with { Capacity = 6 }, Later);

        Assert.Equal("Переговорная «Ладога»", resource.Name);
        Assert.Equal(6, resource.Capacity);
        Assert.Equal(Created, resource.CreatedAt);
        Assert.Equal(Later, resource.UpdatedAt);
    }

    [Fact]
    public void Deactivate_and_activate_toggle_state()
    {
        var resource = new Resource(Details(), Created);

        resource.Deactivate(Later);
        Assert.False(resource.IsActive);
        Assert.Equal(Later, resource.UpdatedAt);

        resource.Activate(Later.AddHours(1));
        Assert.True(resource.IsActive);
    }

    [Fact]
    public void SetFeatures_replaces_list_and_drops_duplicates()
    {
        var projector = new Feature("projector", "Проектор");
        var whiteboard = new Feature("whiteboard", "Маркерная доска");
        var resource = new Resource(Details(), Created);

        resource.SetFeatures([projector, whiteboard, projector]);
        Assert.Equal(2, resource.Features.Count);

        resource.SetFeatures([whiteboard]);
        Assert.Same(whiteboard, Assert.Single(resource.Features));
    }
}
