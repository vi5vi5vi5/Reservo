using Reservo.Domain.Bookings;
using static Reservo.UnitTests.Bookings.BookingTestData;

namespace Reservo.UnitTests.Bookings;

/// <summary>
/// Решения фоновой задачи при разном «текущем времени». Время — просто параметр функции,
/// поэтому ни таймеры, ни база для проверки не нужны.
/// </summary>
public class BookingLifecycleTests
{
    // Бронь из TomorrowAt14: завтра 14:00–15:00 по Москве.
    private static readonly DateTimeOffset Start = Msk(25, 14);
    private static readonly DateTimeOffset End = Msk(25, 15);

    // ---------- Автоотклонение (UC-20) ----------

    [Fact]
    public void Pending_booking_is_rejected_once_its_start_arrives()
    {
        var booking = TomorrowAt14(requiresApproval: true);

        Assert.False(BookingLifecycle.ShouldAutoReject(booking, Start.AddMinutes(-1)));
        Assert.True(BookingLifecycle.ShouldAutoReject(booking, Start));
    }

    [Fact]
    public void Confirmed_booking_is_never_auto_rejected()
    {
        Assert.False(BookingLifecycle.ShouldAutoReject(TomorrowAt14(), End));
    }

    // ---------- Автозавершение (UC-19) ----------

    [Fact]
    public void Confirmed_booking_completes_exactly_at_its_end()
    {
        var booking = TomorrowAt14();

        Assert.False(BookingLifecycle.ShouldComplete(booking, End.AddMinutes(-1)));
        Assert.True(BookingLifecycle.ShouldComplete(booking, End));
    }

    [Fact]
    public void Cancelled_booking_is_not_completed()
    {
        var booking = TomorrowAt14();
        booking.Cancel(null, Now);

        Assert.False(BookingLifecycle.ShouldComplete(booking, End.AddHours(1)));
    }

    // ---------- Напоминание за 30 минут (UC-18) ----------

    [Theory]
    [InlineData(31, false)] // ещё рано
    [InlineData(30, true)]  // ровно за 30 минут
    [InlineData(1, true)]   // за минуту
    [InlineData(0, false)]  // уже началась
    public void Reminder_window_is_30_minutes_before_start(int minutesBeforeStart, bool expected)
    {
        Assert.Equal(expected, BookingLifecycle.ShouldRemind(TomorrowAt14(), Start.AddMinutes(-minutesBeforeStart), Policy));
    }

    [Fact]
    public void Reminder_is_sent_only_once()
    {
        var booking = TomorrowAt14();
        var now = Start.AddMinutes(-20);

        booking.MarkReminderSent(now);

        Assert.False(BookingLifecycle.ShouldRemind(booking, now.AddMinutes(5), Policy));
    }

    [Fact]
    public void Pending_booking_gets_no_reminder()
    {
        Assert.False(BookingLifecycle.ShouldRemind(TomorrowAt14(requiresApproval: true), Start.AddMinutes(-10), Policy));
    }
}
