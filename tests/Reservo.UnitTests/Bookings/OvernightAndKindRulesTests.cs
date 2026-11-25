using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;
using static Reservo.UnitTests.Bookings.BookingTestData;

namespace Reservo.UnitTests.Bookings;

/// <summary>Правила, появившиеся вместе с коворкингами и оборудованием «на ночь».</summary>
public class OvernightAndKindRulesTests
{
    // ---------- BR-06 для ресурса «на ночь»: выдача и возврат 08:00–21:00 ----------

    [Theory]
    [InlineData(25, 18, 0, 26, 9, 0, true)]   // на ночь: вечером взял, утром вернул
    [InlineData(25, 10, 0, 28, 10, 0, true)]  // на трое суток
    [InlineData(25, 20, 45, 26, 8, 0, true)]  // выдали за 15 минут до закрытия, вернули к открытию
    [InlineData(25, 21, 0, 26, 9, 0, false)]  // в момент закрытия выдавать уже некому
    [InlineData(25, 18, 0, 26, 7, 0, false)]  // вернуть до открытия нельзя
    [InlineData(25, 7, 0, 25, 12, 0, false)]  // выдать до открытия нельзя
    [InlineData(25, 18, 0, 26, 22, 0, false)] // вернуть после закрытия нельзя
    public void Overnight_resource_checks_only_pickup_and_return(
        int fromDay, int fromH, int fromM, int toDay, int toH, int toM, bool valid)
    {
        var error = BookingRules.CheckWorkingHours(Msk(fromDay, fromH, fromM), Msk(toDay, toH, toM), Laptop(), Policy);

        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void Overnight_violation_explains_pickup_hours()
    {
        var error = BookingRules.CheckWorkingHours(Msk(25, 18), Msk(26, 7), Laptop(), Policy);

        Assert.Equal("booking.outside_working_hours", error?.Code);
        Assert.Contains("выдача и возврат", error!.Message);
    }

    [Fact]
    public void Three_day_booking_passes_all_slot_checks_for_overnight_resource()
    {
        Assert.Null(BookingRules.CheckSlot(Msk(25, 10), Msk(28, 10), null, Laptop(), Policy, Now));
    }

    [Fact]
    public void Overnight_booking_still_respects_resource_limit()
    {
        var error = BookingRules.CheckSlot(Msk(25, 10), Msk(29, 10), null, Laptop(), Policy, Now);

        Assert.Equal("booking.duration_out_of_range", error?.Code);
    }

    [Fact]
    public void Regular_resource_still_cannot_cross_midnight()
    {
        Assert.Equal("booking.outside_working_hours", BookingRules.CheckWorkingHours(Msk(25, 18), Msk(26, 9), Room(), Policy)?.Code);
    }

    // ---------- Тема брони ----------

    [Theory]
    [InlineData(ResourceKind.Room, null, false)]
    [InlineData(ResourceKind.Room, "   ", false)]
    [InlineData(ResourceKind.Room, "Встреча", true)]
    [InlineData(ResourceKind.Equipment, null, false)]
    [InlineData(ResourceKind.Seat, null, true)]
    [InlineData(ResourceKind.Seat, "Подготовка к экзамену", true)]
    public void Title_is_optional_only_for_seats(ResourceKind kind, string? title, bool valid)
    {
        Assert.Equal(valid, BookingRules.CheckTitle(title, kind) is null);
    }

    // ---------- BR-11: что занимает самого человека ----------

    [Theory]
    [InlineData(ResourceKind.Room, true)]
    [InlineData(ResourceKind.Seat, true)]
    [InlineData(ResourceKind.Equipment, false)]
    public void Equipment_does_not_occupy_person(ResourceKind kind, bool occupies)
    {
        Assert.Equal(occupies, kind.OccupiesPerson());
    }

    // ---------- Пустая тема в сущности ----------

    [Fact]
    public void Blank_title_is_stored_as_null()
    {
        var booking = new Booking(1, 7, Msk(25, 14), Msk(25, 15), "   ", null, requiresApproval: false, Now);

        Assert.Null(booking.Title);
    }
}
