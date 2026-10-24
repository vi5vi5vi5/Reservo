using Reservo.Domain.Bookings;
using static Reservo.UnitTests.Bookings.BookingTestData;

namespace Reservo.UnitTests.Bookings;

public class BookingRulesTests
{
    // ---------- Пересечение интервалов (основа BR-09 и BR-11) ----------

    [Theory]
    [InlineData(10, 11, 11, 12, false)] // смежные: конец одной = начало другой
    [InlineData(11, 12, 10, 11, false)] // смежные в обратном порядке
    [InlineData(10, 11, 12, 13, false)] // далеко друг от друга
    [InlineData(10, 12, 11, 13, true)]  // частичное перекрытие
    [InlineData(10, 13, 11, 12, true)]  // одна внутри другой
    [InlineData(10, 11, 10, 11, true)]  // совпадают
    public void Overlaps_uses_half_open_intervals(int aFrom, int aTo, int bFrom, int bTo, bool expected)
    {
        Assert.Equal(expected, BookingRules.Overlaps(Msk(25, aFrom), Msk(25, aTo), Msk(25, bFrom), Msk(25, bTo)));
    }

    // ---------- BR-02: сетка 15 минут ----------

    [Fact]
    public void Times_on_the_grid_pass()
    {
        Assert.Null(BookingRules.CheckTimeGrid(Msk(25, 10, 15), Msk(25, 10, 45), Policy));
    }

    [Theory]
    [InlineData(10, 5, 0)]
    [InlineData(10, 0, 30)]
    public void Start_off_the_grid_fails(int hour, int minute, int second)
    {
        var error = BookingRules.CheckTimeGrid(Msk(25, hour, minute, second), Msk(25, 11), Policy);

        Assert.Equal("booking.invalid_time_grid", error?.Code);
    }

    [Fact]
    public void Grid_is_checked_in_local_time_whatever_offset_the_client_used()
    {
        // 07:15 UTC = 10:15 по Москве — на сетке.
        var start = new DateTimeOffset(2026, 10, 25, 7, 15, 0, TimeSpan.Zero);

        Assert.Null(BookingRules.CheckTimeGrid(start, start.AddMinutes(30), Policy));
    }

    // ---------- BR-03: только будущее ----------

    [Fact]
    public void Start_exactly_now_fails()
    {
        Assert.Equal("booking.in_past", BookingRules.CheckStartsInFuture(Now, Now)?.Code);
    }

    [Fact]
    public void Start_in_the_future_passes()
    {
        Assert.Null(BookingRules.CheckStartsInFuture(Now.AddMinutes(15), Now));
    }

    // ---------- BR-04: не дальше 30 дней ----------

    [Fact]
    public void Start_exactly_30_days_ahead_passes()
    {
        Assert.Null(BookingRules.CheckAdvance(Now.AddDays(30), Now, Policy));
    }

    [Fact]
    public void Start_more_than_30_days_ahead_fails()
    {
        Assert.Equal("booking.too_far_in_advance", BookingRules.CheckAdvance(Now.AddDays(30).AddMinutes(15), Now, Policy)?.Code);
    }

    // ---------- BR-05: длительность ----------

    [Theory]
    [InlineData(15, true)]   // ровно минимум
    [InlineData(240, true)]  // ровно общий максимум
    [InlineData(10, false)]  // меньше минимума
    [InlineData(255, false)] // больше максимума
    public void Duration_uses_global_limits(int minutes, bool valid)
    {
        var error = BookingRules.CheckDuration(Msk(25, 9), Msk(25, 9).AddMinutes(minutes), Room(), Policy);

        Assert.Equal(valid, error is null);
    }

    [Theory]
    [InlineData(120, true)]
    [InlineData(135, false)]
    public void Duration_uses_resource_limit_when_set(int minutes, bool valid)
    {
        var error = BookingRules.CheckDuration(Msk(25, 9), Msk(25, 9).AddMinutes(minutes), Room(maxDurationMinutes: 120), Policy);

        Assert.Equal(valid, error is null);
    }

    // ---------- BR-06: рабочие часы 08:00–21:00 ----------

    [Theory]
    [InlineData(8, 0, 9, 0, true)]    // начинается ровно в открытие
    [InlineData(20, 0, 21, 0, true)]  // заканчивается ровно в закрытие
    [InlineData(7, 45, 8, 30, false)] // начинается до открытия
    [InlineData(20, 30, 21, 15, false)] // заканчивается после закрытия
    public void Booking_must_fit_working_hours(int fromH, int fromM, int toH, int toM, bool valid)
    {
        var error = BookingRules.CheckWorkingHours(Msk(25, fromH, fromM), Msk(25, toH, toM), Room(), Policy);

        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void Booking_over_midnight_fails()
    {
        var error = BookingRules.CheckWorkingHours(Msk(25, 20), Msk(26, 9), Room(), Policy);

        Assert.Equal("booking.outside_working_hours", error?.Code);
    }

    [Fact]
    public void Working_hours_are_compared_in_local_time()
    {
        // 05:00–06:00 UTC = 08:00–09:00 по Москве: ровно с открытия.
        var start = new DateTimeOffset(2026, 10, 25, 5, 0, 0, TimeSpan.Zero);

        Assert.Null(BookingRules.CheckWorkingHours(start, start.AddHours(1), Room(), Policy));
    }

    // ---------- BR-07 и BR-08 ----------

    [Fact]
    public void Inactive_resource_fails()
    {
        var resource = Room();
        resource.Deactivate(Now);

        Assert.Equal("resource.inactive", BookingRules.CheckResourceActive(resource)?.Code);
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(9, false)]
    [InlineData(null, true)]
    public void Attendees_must_fit_capacity(int? attendees, bool valid)
    {
        Assert.Equal(valid, BookingRules.CheckCapacity(attendees, Room(capacity: 8)) is null);
    }

    [Fact]
    public void Capacity_is_not_checked_for_equipment()
    {
        Assert.Null(BookingRules.CheckCapacity(50, Room(capacity: null)));
    }

    // ---------- Сводная проверка интервала ----------

    [Fact]
    public void Valid_slot_passes_all_checks()
    {
        Assert.Null(BookingRules.CheckSlot(Msk(25, 14), Msk(25, 15, 30), 6, Room(), Policy, Now));
    }

    [Fact]
    public void Slot_check_reports_the_first_violation()
    {
        var resource = Room();
        resource.Deactivate(Now);

        // Время тоже неверное (в прошлом), но первым сообщается, что ресурс выведен из эксплуатации.
        var error = BookingRules.CheckSlot(Msk(23, 14), Msk(23, 15), 6, resource, Policy, Now);

        Assert.Equal("resource.inactive", error?.Code);
    }

    // ---------- BR-09, BR-10, BR-11 ----------

    [Fact]
    public void Time_conflict_names_busy_time_in_local_time()
    {
        var busyFrom = new DateTimeOffset(2026, 10, 25, 11, 0, 0, TimeSpan.Zero); // 14:00 МСК
        var error = BookingRules.TimeConflict("Переговорная «Байкал»", busyFrom, busyFrom.AddHours(1), Policy);

        Assert.Equal("booking.time_conflict", error.Code);
        Assert.True(error.IsConflict);
        Assert.Contains("25 октября с 14:00 до 15:00", error.Detail);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void Active_bookings_limit(int activeBookings, bool valid)
    {
        Assert.Equal(valid, BookingRules.CheckUserLimit(activeBookings, Policy) is null);
    }

    // ---------- BR-12: изменение ----------

    [Fact]
    public void Future_active_booking_can_be_modified()
    {
        Assert.Null(BookingRules.CheckCanModify(TomorrowAt14(), Now));
    }

    [Fact]
    public void Started_booking_cannot_be_modified()
    {
        var booking = TomorrowAt14();

        Assert.Equal("booking.already_started", BookingRules.CheckCanModify(booking, booking.StartAt)?.Code);
    }

    [Fact]
    public void Cancelled_booking_cannot_be_modified()
    {
        var booking = TomorrowAt14();
        booking.Cancel(null, Now);

        Assert.Equal("booking.invalid_status_transition", BookingRules.CheckCanModify(booking, Now)?.Code);
    }

    // ---------- BR-13: отмена ----------

    [Fact]
    public void Owner_cancels_before_start()
    {
        Assert.Null(BookingRules.CheckCanCancel(TomorrowAt14(), byAdmin: false, Now));
    }

    [Fact]
    public void Owner_cannot_cancel_after_start()
    {
        var booking = TomorrowAt14();

        Assert.Equal("booking.already_started", BookingRules.CheckCanCancel(booking, byAdmin: false, booking.StartAt.AddMinutes(10))?.Code);
    }

    [Fact]
    public void Admin_cancels_running_booking_but_not_finished_one()
    {
        var booking = TomorrowAt14();

        Assert.Null(BookingRules.CheckCanCancel(booking, byAdmin: true, booking.StartAt.AddMinutes(10)));
        Assert.Equal("booking.already_finished", BookingRules.CheckCanCancel(booking, byAdmin: true, booking.EndAt)?.Code);
    }

    [Fact]
    public void Cancelled_booking_cannot_be_cancelled_again()
    {
        var booking = TomorrowAt14();
        booking.Cancel(null, Now);

        Assert.Equal("booking.invalid_status_transition", BookingRules.CheckCanCancel(booking, byAdmin: true, Now)?.Code);
    }

    // ---------- Согласование ----------

    [Fact]
    public void Pending_booking_can_be_approved_before_start()
    {
        Assert.Null(BookingRules.CheckCanApprove(TomorrowAt14(requiresApproval: true), Now));
    }

    [Fact]
    public void Confirmed_booking_cannot_be_approved_or_rejected()
    {
        var booking = TomorrowAt14(requiresApproval: false);

        Assert.Equal("booking.invalid_status_transition", BookingRules.CheckCanApprove(booking, Now)?.Code);
        Assert.Equal("booking.invalid_status_transition", BookingRules.CheckCanReject(booking)?.Code);
    }

    [Fact]
    public void Started_pending_booking_cannot_be_approved()
    {
        var booking = TomorrowAt14(requiresApproval: true);

        Assert.Equal("booking.already_started", BookingRules.CheckCanApprove(booking, booking.StartAt)?.Code);
    }
}
