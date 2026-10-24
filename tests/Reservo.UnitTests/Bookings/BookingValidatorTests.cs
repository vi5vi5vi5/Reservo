using FluentValidation.TestHelper;
using Reservo.Application.Bookings;
using static Reservo.UnitTests.Bookings.BookingTestData;

namespace Reservo.UnitTests.Bookings;

public class BookingValidatorTests
{
    private readonly CreateBookingRequestValidator _createValidator = new();
    private readonly RejectBookingRequestValidator _rejectValidator = new();
    private readonly BookingListQueryValidator _listValidator = new();
    private readonly AvailableResourcesQueryValidator _availableValidator = new();
    private readonly ScheduleQueryValidator _scheduleValidator = new();

    private static CreateBookingRequest Valid() => new()
    {
        ResourceId = 1,
        StartAt = Msk(25, 14),
        EndAt = Msk(25, 15, 30),
        Title = "Встреча проектной команды",
        Attendees = 6,
    };

    [Fact]
    public void Valid_request_passes()
    {
        _createValidator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void End_not_after_start_fails(int minutesAfterStart)
    {
        _createValidator.TestValidate(Valid() with { EndAt = Msk(25, 14).AddMinutes(minutesAfterStart) })
            .ShouldHaveValidationErrorFor(x => x.EndAt)
            .WithErrorMessage("Конец брони должен быть позже начала.");
    }

    [Fact]
    public void Missing_fields_fail()
    {
        var result = _createValidator.TestValidate(new CreateBookingRequest());

        result.ShouldHaveValidationErrorFor(x => x.ResourceId);
        result.ShouldHaveValidationErrorFor(x => x.StartAt);
        result.ShouldHaveValidationErrorFor(x => x.EndAt);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Zero_attendees_fail()
    {
        _createValidator.TestValidate(Valid() with { Attendees = 0 })
            .ShouldHaveValidationErrorFor(x => x.Attendees);
    }

    [Fact]
    public void Reject_requires_reason()
    {
        _rejectValidator.TestValidate(new RejectBookingRequest { Reason = " " })
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void List_window_must_be_ordered()
    {
        _listValidator.TestValidate(new BookingListQuery { From = Msk(25, 10), To = Msk(25, 9) })
            .ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Available_search_needs_both_ends()
    {
        var result = _availableValidator.TestValidate(new AvailableResourcesQuery());

        result.ShouldHaveValidationErrorFor(x => x.From);
        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Schedule_window_is_limited_to_31_days()
    {
        _scheduleValidator.TestValidate(new ScheduleQuery { From = Msk(1, 0), To = Msk(1, 0).AddDays(32) })
            .ShouldHaveValidationErrorFor(x => x.To);

        _scheduleValidator.TestValidate(new ScheduleQuery { From = Msk(1, 0), To = Msk(1, 0).AddDays(31) })
            .ShouldNotHaveAnyValidationErrors();
    }
}
