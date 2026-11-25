using Microsoft.EntityFrameworkCore;
using Reservo.Application.Common;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Admin;

/// <summary>Строка тепловой карты загрузки.</summary>
/// <param name="Type"><c>resource</c> — отдельный ресурс, <c>space</c> — коворкинг целиком (все его места вместе).</param>
/// <param name="Id">Id ресурса или коворкинга.</param>
/// <param name="Name">Название.</param>
/// <param name="CategoryCode">Код категории ресурса — для иконки. У коворкинга пусто.</param>
/// <param name="BusyPercent">По часу на элемент массива <c>Hours</c>: сколько процентов часа занято (у коворкинга — в среднем по местам). Пусто — в этот час ресурс не работает.</param>
public sealed record HeatmapRowDto(string Type, int Id, string Name, string? CategoryCode, IReadOnlyList<int?> BusyPercent);

/// <summary>Сводка для дашборда администратора.</summary>
/// <param name="Date">День сводки, местная дата площадки.</param>
/// <param name="TimeZone">Часовой пояс площадки.</param>
/// <param name="GeneratedAt">Момент, на который посчитано «идёт сейчас» и срочность заявок (UTC).</param>
/// <param name="BookingsOnDate">Броней в этот день (ждут согласования, подтверждены или завершены).</param>
/// <param name="InProgressNow">Подтверждённых броней, которые идут прямо сейчас.</param>
/// <param name="PendingApprovals">Заявок ждут согласования (все будущие, не только за этот день).</param>
/// <param name="UrgentApprovals">Из них начнутся в ближайшие <c>UrgentHours</c> часа.</param>
/// <param name="UrgentHours">Порог срочности в часах.</param>
/// <param name="ActiveResources">Ресурсов в эксплуатации.</param>
/// <param name="InactiveResources">Выведено из эксплуатации.</param>
/// <param name="Hours">Часы тепловой карты по местному времени: 8 — это 08:00–09:00.</param>
/// <param name="Heatmap">Загрузка ресурсов и коворкингов по часам.</param>
public sealed record AdminStatsDto(
    DateOnly Date,
    string TimeZone,
    DateTimeOffset GeneratedAt,
    int BookingsOnDate,
    int InProgressNow,
    int PendingApprovals,
    int UrgentApprovals,
    int UrgentHours,
    int ActiveResources,
    int InactiveResources,
    IReadOnlyList<int> Hours,
    IReadOnlyList<HeatmapRowDto> Heatmap);

/// <summary>Параметры сводки.</summary>
public sealed record AdminStatsQuery
{
    /// <summary>День сводки (<c>2026-10-26</c>), по умолчанию — сегодня по местному времени площадки.</summary>
    public DateOnly? Date { get; init; }
}

public interface IAdminStatsService
{
    Task<AdminStatsDto> GetAsync(AdminStatsQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Дашборд администратора (UC-25): главные числа дня и тепловая карта «ресурс × час». Ближайшие брони и последние
/// события фронтенд берёт из существующих списков (<c>/api/admin/bookings</c>, <c>/api/admin/audit</c>).
/// Броней за день немного, поэтому они загружаются одним запросом и считаются в памяти.
/// </summary>
public sealed class AdminStatsService(IAppDbContext db, BookingPolicy policy, TimeProvider timeProvider) : IAdminStatsService
{
    public const int UrgentHours = 3;

    public async Task<AdminStatsDto> GetAsync(AdminStatsQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var date = query.Date ?? DateOnly.FromDateTime(policy.ToLocal(now).Date);
        var day = date.ToDateTime(TimeOnly.MinValue);
        var dayStart = policy.AtLocal(day, TimeOnly.MinValue);
        var dayEnd = policy.AtLocal(day.AddDays(1), TimeOnly.MinValue);

        var bookings = await db.Bookings
            .AsNoTracking()
            .Where(b => (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
                        && b.StartAt < dayEnd && dayStart < b.EndAt)
            .Select(b => new { b.ResourceId, b.Status, b.StartAt, b.EndAt })
            .ToListAsync(cancellationToken);

        var urgentUntil = now.AddHours(UrgentHours);
        var pending = await db.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending && b.StartAt > now)
            .Select(b => b.StartAt)
            .ToListAsync(cancellationToken);

        var resources = await db.Resources
            .AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.IsActive,
                r.OpenTime,
                r.CloseTime,
                r.SpaceId,
                SpaceName = r.Space != null ? r.Space.Name : null,
                CategoryCode = r.Category.Code,
                CategoryName = r.Category.Name,
            })
            .ToListAsync(cancellationToken);

        var active = resources.Where(r => r.IsActive).ToList();

        // Часы карты — от самого раннего открытия до самого позднего закрытия среди работающих ресурсов.
        var firstHour = active.Count > 0 ? active.Min(r => r.OpenTime.Hour) : 8;
        var lastHour = active.Count > 0 ? active.Max(r => r.CloseTime.Minute > 0 ? r.CloseTime.Hour + 1 : r.CloseTime.Hour) : 21;
        var hours = Enumerable.Range(firstHour, Math.Max(0, lastHour - firstHour)).ToList();

        var busyByResource = bookings.ToLookup(b => b.ResourceId);

        // Сколько минут часа [hour, hour+1) ресурс работает и сколько из них занято.
        (int Open, int Busy) Cell(int resourceId, TimeOnly open, TimeOnly close, int hour)
        {
            var from = policy.AtLocal(day, new TimeOnly(hour, 0));
            var to = from.AddHours(1);
            var openFrom = Max(from, policy.AtLocal(day, open));
            var openTo = Min(to, policy.AtLocal(day, close));
            var openMinutes = openTo > openFrom ? (int)(openTo - openFrom).TotalMinutes : 0;

            var busy = busyByResource[resourceId].Sum(b =>
            {
                var s = Max(b.StartAt, openFrom);
                var e = Min(b.EndAt, openTo);
                return e > s ? (int)(e - s).TotalMinutes : 0;
            });

            return (openMinutes, Math.Min(busy, openMinutes));
        }

        static int? Percent(int open, int busy) => open == 0 ? null : (int)Math.Round(100.0 * busy / open);

        var rows = active
            .Where(r => r.SpaceId is null)
            .OrderBy(r => r.CategoryName).ThenBy(r => r.Name)
            .Select(r => new HeatmapRowDto("resource", r.Id, r.Name, r.CategoryCode,
                hours.Select(h => { var c = Cell(r.Id, r.OpenTime, r.CloseTime, h); return Percent(c.Open, c.Busy); }).ToList()))
            .ToList();

        rows.AddRange(active
            .Where(r => r.SpaceId is not null)
            .GroupBy(r => (Id: r.SpaceId!.Value, Name: r.SpaceName!))
            .OrderBy(g => g.Key.Name)
            .Select(g => new HeatmapRowDto("space", g.Key.Id, g.Key.Name, null,
                hours.Select(h =>
                {
                    var cells = g.Select(r => Cell(r.Id, r.OpenTime, r.CloseTime, h)).ToList();
                    return Percent(cells.Sum(c => c.Open), cells.Sum(c => c.Busy));
                }).ToList())));

        return new AdminStatsDto(
            date,
            policy.TimeZone.Id,
            now,
            bookings.Count,
            bookings.Count(b => b.Status == BookingStatus.Confirmed && b.StartAt <= now && now < b.EndAt),
            pending.Count,
            pending.Count(start => start <= urgentUntil),
            UrgentHours,
            active.Count,
            resources.Count - active.Count,
            hours,
            rows);
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
