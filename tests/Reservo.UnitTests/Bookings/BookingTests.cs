using Reservo.Domain.Bookings;
using static Reservo.UnitTests.Bookings.BookingTestData;

namespace Reservo.UnitTests.Bookings;

public class BookingTests
{
    [Fact]
    public void Booking_of_resource_without_approval_is_confirmed_at_once()
    {
        Assert.Equal(BookingStatus.Confirmed, TomorrowAt14(requiresApproval: false).Status);
    }

    [Fact]
    public void Booking_of_resource_with_approval_waits_for_admin()
    {
        Assert.Equal(BookingStatus.Pending, TomorrowAt14(requiresApproval: true).Status);
    }

    [Fact]
    public void Times_are_stored_in_utc()
    {
        var booking = TomorrowAt14();

        Assert.Equal(TimeSpan.Zero, booking.StartAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 11, 0, 0, TimeSpan.Zero), booking.StartAt);
    }

    [Fact]
    public void Rescheduling_confirmed_booking_of_approval_resource_sends_it_back_to_approval()
    {
        var booking = TomorrowAt14(requiresApproval: true);
        booking.Approve(Now);

        booking.Update(Msk(25, 16), Msk(25, 17), "Встреча", 4, requiresApproval: true, Now);

        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void Changing_only_title_keeps_confirmation()
    {
        var booking = TomorrowAt14(requiresApproval: true);
        booking.Approve(Now);

        // То же время, но записанное в UTC: это тот же момент, значит время не изменилось.
        booking.Update(booking.StartAt.ToOffset(TimeSpan.Zero), booking.EndAt, "Новая тема", 6, requiresApproval: true, Now);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal("Новая тема", booking.Title);
        Assert.Equal(6, booking.Attendees);
    }

    [Fact]
    public void Rescheduling_without_approval_keeps_confirmation()
    {
        var booking = TomorrowAt14(requiresApproval: false);

        booking.Update(Msk(25, 16), Msk(25, 17), "Встреча", 4, requiresApproval: false, Now);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void Reject_and_cancel_store_reason()
    {
        var rejected = TomorrowAt14(requiresApproval: true);
        rejected.Reject("  Лаборатория занята  ", Now);

        var cancelled = TomorrowAt14();
        cancelled.Cancel("   ", Now);

        Assert.Equal(BookingStatus.Rejected, rejected.Status);
        Assert.Equal("Лаборатория занята", rejected.StatusReason);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.StatusReason);
    }

    [Fact]
    public void Only_confirmed_booking_can_be_completed()
    {
        var pending = TomorrowAt14(requiresApproval: true);
        var confirmed = TomorrowAt14();

        confirmed.Complete(Now);

        Assert.Equal(BookingStatus.Completed, confirmed.Status);
        Assert.Throws<InvalidOperationException>(() => pending.Complete(Now));
    }

    [Fact]
    public void Inactive_booking_cannot_be_updated()
    {
        var booking = TomorrowAt14();
        booking.Cancel(null, Now);

        Assert.Throws<InvalidOperationException>(() => booking.Update(Msk(25, 16), Msk(25, 17), "x", null, false, Now));
    }
}
