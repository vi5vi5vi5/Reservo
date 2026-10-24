using FluentValidation.TestHelper;
using Reservo.Application.Catalog.Resources;

namespace Reservo.UnitTests.Catalog;

public class ResourceValidatorTests
{
    private readonly ResourceRequestValidator _requestValidator = new();
    private readonly ResourceListQueryValidator _queryValidator = new();
    private readonly DeactivateResourceRequestValidator _deactivateValidator = new();

    private static ResourceRequest Valid() => new()
    {
        CategoryId = 1,
        LocationId = 1,
        Name = "Переговорная «Таймыр»",
        Capacity = 6,
        OpenTime = new TimeOnly(8, 0),
        CloseTime = new TimeOnly(21, 0),
        FeatureIds = [1, 2],
    };

    [Fact]
    public void Valid_request_passes()
    {
        _requestValidator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Equipment_without_capacity_and_floor_passes()
    {
        _requestValidator.TestValidate(Valid() with { Capacity = null, Floor = null, FeatureIds = [] })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Missing_category_location_and_name_fail()
    {
        var result = _requestValidator.TestValidate(Valid() with { CategoryId = 0, LocationId = 0, Name = " " });

        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
        result.ShouldHaveValidationErrorFor(x => x.LocationId);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(21, 8)]
    [InlineData(9, 9)]
    public void Close_time_not_after_open_time_fails(int openHour, int closeHour)
    {
        _requestValidator.TestValidate(Valid() with { OpenTime = new TimeOnly(openHour, 0), CloseTime = new TimeOnly(closeHour, 0) })
            .ShouldHaveValidationErrorFor(x => x.CloseTime)
            .WithErrorMessage("Время закрытия должно быть позже времени открытия.");
    }

    [Fact]
    public void Missing_working_hours_report_both_fields()
    {
        var result = _requestValidator.TestValidate(Valid() with { OpenTime = null, CloseTime = null });

        result.ShouldHaveValidationErrorFor(x => x.OpenTime);
        result.ShouldHaveValidationErrorFor(x => x.CloseTime).WithErrorMessage("Укажите время закрытия.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Capacity_out_of_range_fails(int capacity)
    {
        _requestValidator.TestValidate(Valid() with { Capacity = capacity })
            .ShouldHaveValidationErrorFor(x => x.Capacity);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(1441)]
    public void Booking_limit_out_of_range_fails(int minutes)
    {
        _requestValidator.TestValidate(Valid() with { MaxDurationMinutes = minutes })
            .ShouldHaveValidationErrorFor(x => x.MaxDurationMinutes);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://files.example/photo.jpg")]
    [InlineData("/images/room.jpg")]
    public void Image_url_must_be_absolute_http(string url)
    {
        _requestValidator.TestValidate(Valid() with { ImageUrl = url })
            .ShouldHaveValidationErrorFor(x => x.ImageUrl);
    }

    [Fact]
    public void Https_image_url_passes()
    {
        _requestValidator.TestValidate(Valid() with { ImageUrl = "https://images.example/room.jpg" })
            .ShouldNotHaveValidationErrorFor(x => x.ImageUrl);
    }

    [Fact]
    public void Non_positive_feature_id_fails()
    {
        _requestValidator.TestValidate(Valid() with { FeatureIds = [1, 0] })
            .ShouldHaveValidationErrorFor("FeatureIds[1]");
    }

    [Fact]
    public void Too_many_features_fail()
    {
        _requestValidator.TestValidate(Valid() with { FeatureIds = Enumerable.Range(1, 31).ToArray() })
            .ShouldHaveValidationErrorFor(x => x.FeatureIds);
    }

    [Fact]
    public void Default_list_query_is_valid()
    {
        _queryValidator.TestValidate(new ResourceListQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_list_filters_fail()
    {
        var result = _queryValidator.TestValidate(new ResourceListQuery
        {
            Page = 0,
            MinCapacity = 0,
            CategoryId = -1,
            FeatureIds = Enumerable.Range(1, 21).ToArray(),
        });

        result.ShouldHaveValidationErrorFor(x => x.Page);
        result.ShouldHaveValidationErrorFor(x => x.MinCapacity);
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
        result.ShouldHaveValidationErrorFor(x => x.FeatureIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deactivation_requires_reason(string reason)
    {
        _deactivateValidator.TestValidate(new DeactivateResourceRequest { Reason = reason })
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }
}
