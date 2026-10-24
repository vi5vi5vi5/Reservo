using FluentValidation.TestHelper;
using Reservo.Application.Catalog.Categories;
using Reservo.Application.Catalog.Features;
using Reservo.Application.Catalog.Locations;

namespace Reservo.UnitTests.Catalog;

public class DictionaryValidatorTests
{
    private readonly CategoryRequestValidator _categoryValidator = new();
    private readonly FeatureRequestValidator _featureValidator = new();
    private readonly LocationRequestValidator _locationValidator = new();

    [Theory]
    [InlineData("meeting-room")]
    [InlineData("lab")]
    [InlineData("3d-printer")]
    public void Valid_code_passes(string code)
    {
        _categoryValidator.TestValidate(new CategoryRequest { Code = code, Name = "Название" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Meeting-Room")]
    [InlineData("meeting room")]
    [InlineData("meeting_room")]
    [InlineData("-room")]
    [InlineData("room-")]
    [InlineData("room--1")]
    [InlineData("переговорная")]
    public void Invalid_code_fails(string code)
    {
        _categoryValidator.TestValidate(new CategoryRequest { Code = code, Name = "Название" })
            .ShouldHaveValidationErrorFor(x => x.Code);

        _featureValidator.TestValidate(new FeatureRequest { Code = code, Name = "Название" })
            .ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Code_longer_than_50_characters_fails()
    {
        _categoryValidator.TestValidate(new CategoryRequest { Code = new string('a', 51), Name = "Название" })
            .ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_name_fails(string name)
    {
        _categoryValidator.TestValidate(new CategoryRequest { Code = "lab", Name = name })
            .ShouldHaveValidationErrorFor(x => x.Name);

        _featureValidator.TestValidate(new FeatureRequest { Code = "lab", Name = name })
            .ShouldHaveValidationErrorFor(x => x.Name);

        _locationValidator.TestValidate(new LocationRequest { Name = name })
            .ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Location_without_address_passes()
    {
        _locationValidator.TestValidate(new LocationRequest { Name = "Корпус В" })
            .ShouldNotHaveAnyValidationErrors();
    }
}
