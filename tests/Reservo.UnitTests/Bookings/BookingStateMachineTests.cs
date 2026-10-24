using Reservo.Domain.Bookings;
using static Reservo.UnitTests.Bookings.BookingTestData;

namespace Reservo.UnitTests.Bookings;

public class BookingStateMachineTests
{
    [Theory]
    [InlineData(BookingStatus.Pending, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Pending, BookingStatus.Rejected)]
    [InlineData(BookingStatus.Pending, BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Pending)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Completed)]
    public void Allowed_transitions(BookingStatus from, BookingStatus to)
    {
        Assert.True(BookingStateMachine.CanTransition(from, to));
        Assert.Null(BookingStateMachine.CheckTransition(from, to));
    }

    [Theory]
    [InlineData(BookingStatus.Pending, BookingStatus.Completed)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Rejected)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected, BookingStatus.Pending)]
    [InlineData(BookingStatus.Completed, BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Confirmed)]
    public void Forbidden_transitions(BookingStatus from, BookingStatus to)
    {
        Assert.False(BookingStateMachine.CanTransition(from, to));
        Assert.Equal("booking.invalid_status_transition", BookingStateMachine.CheckTransition(from, to)?.Code);
    }

    [Theory]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Completed)]
    public void Final_statuses_have_no_way_out(BookingStatus final)
    {
        foreach (var target in Enum.GetValues<BookingStatus>())
        {
            Assert.False(BookingStateMachine.CanTransition(final, target));
        }
    }

    [Fact]
    public void Only_pending_and_confirmed_are_active()
    {
        var active = Enum.GetValues<BookingStatus>().Where(s => s.IsActive());

        Assert.Equal(new[] { BookingStatus.Pending, BookingStatus.Confirmed }, active);
    }

    [Fact]
    public void Entity_refuses_forbidden_transition_as_a_programming_error()
    {
        var booking = TomorrowAt14();
        booking.Cancel(null, Now);

        // Сервис обязан проверить правило заранее; если не проверил — это ошибка в коде, а не бизнес-ошибка.
        Assert.Throws<InvalidOperationException>(() => booking.Cancel(null, Now));
        Assert.Throws<InvalidOperationException>(() => booking.Approve(Now));
    }
}
