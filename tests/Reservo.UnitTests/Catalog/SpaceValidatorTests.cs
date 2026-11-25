using FluentValidation.TestHelper;
using Reservo.Application.Bookings;
using Reservo.Application.Catalog.Categories;
using Reservo.Application.Catalog.Spaces;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;

namespace Reservo.UnitTests.Catalog;

/// <summary>Валидаторы коворкингов, подсказок и новых фильтров.</summary>
public class SpaceValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 25, 10, 0, 0, TimeSpan.FromHours(3));

    private static SpaceRequest Valid() => new()
    {
        LocationId = 1,
        Name = "Коворкинг «Точка»",
        Rows = 4,
        Columns = 6,
        AisleAfterColumn = 3,
    };

    [Fact]
    public void Valid_space_passes()
    {
        new SpaceRequestValidator().TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, 6, null)]
    [InlineData(4, 0, null)]
    [InlineData(4, 31, null)]
    [InlineData(4, 6, 6)] // проход за последней колонкой — не проход
    [InlineData(4, 6, 0)]
    public void Invalid_layout_fails(int rows, int columns, int? aisle)
    {
        var result = new SpaceRequestValidator().TestValidate(Valid() with { Rows = rows, Columns = columns, AisleAfterColumn = aisle });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Availability_window_is_optional_but_needs_both_ends()
    {
        var validator = new SpaceAvailabilityQueryValidator();

        validator.TestValidate(new SpaceAvailabilityQuery()).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new SpaceAvailabilityQuery { From = From, To = From.AddHours(2) }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new SpaceAvailabilityQuery { From = From }).ShouldHaveValidationErrorFor("from");
        validator.TestValidate(new SpaceAvailabilityQuery { From = From, To = From.AddDays(2) }).ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Suggestion_query_requires_valid_interval()
    {
        var validator = new SuggestionQueryValidator();

        validator.TestValidate(new SuggestionQuery { StartAt = From, EndAt = From.AddHours(1), Attendees = 4 })
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new SuggestionQuery()).ShouldHaveValidationErrorFor(x => x.StartAt);
        validator.TestValidate(new SuggestionQuery { StartAt = From, EndAt = From }).ShouldHaveValidationErrorFor(x => x.EndAt);
        validator.TestValidate(new SuggestionQuery { StartAt = From, EndAt = From.AddHours(1), Attendees = 0 })
            .ShouldHaveValidationErrorFor(x => x.Attendees);
    }

    [Fact]
    public void Category_kind_must_be_known()
    {
        new CategoryRequestValidator().TestValidate(new CategoryRequest { Code = "coworking", Name = "Места", Kind = (ResourceKind)9 })
            .ShouldHaveValidationErrorFor(x => x.Kind);
    }

    [Fact]
    public void Booking_list_accepts_several_statuses()
    {
        new BookingListQueryValidator()
            .TestValidate(new BookingListQuery { Status = [BookingStatus.Cancelled, BookingStatus.Rejected] })
            .ShouldNotHaveAnyValidationErrors();
    }
}
