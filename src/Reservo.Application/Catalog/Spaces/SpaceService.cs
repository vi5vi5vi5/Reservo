using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Auth;
using Reservo.Application.Catalog.Locations;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Spaces;

public interface ISpaceService
{
    Task<IReadOnlyList<SpaceDto>> GetAllAsync(SpaceAvailabilityQuery query, CancellationToken cancellationToken);

    Task<SpaceDto> GetByIdAsync(int id, SpaceAvailabilityQuery query, CancellationToken cancellationToken);

    Task<SpaceDto> CreateAsync(SpaceRequest request, CancellationToken cancellationToken);

    Task<SpaceDto> UpdateAsync(int id, SpaceRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}

/// <remarks>
/// Схема зала собирается тремя запросами на все коворкинги сразу: сами коворкинги, их места, активные брони мест
/// на интервал. Состояние каждого места решают те же правила, что и при бронировании (BookingRules), поэтому
/// «свободно» на схеме означает: бронь на этот интервал пройдёт, если её никто не опередит.
/// </remarks>
public sealed class SpaceService(
    IAppDbContext db,
    ICurrentUser currentUser,
    BookingPolicy policy,
    TimeProvider timeProvider,
    ILogger<SpaceService> logger) : ISpaceService
{
    public async Task<IReadOnlyList<SpaceDto>> GetAllAsync(SpaceAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var spaces = db.Spaces.AsNoTracking();
        if (query.LocationId is { } locationId)
        {
            spaces = spaces.Where(s => s.LocationId == locationId);
        }

        return await BuildAsync(spaces, query, cancellationToken);
    }

    public async Task<SpaceDto> GetByIdAsync(int id, SpaceAvailabilityQuery query, CancellationToken cancellationToken) =>
        (await BuildAsync(db.Spaces.AsNoTracking().Where(s => s.Id == id), query, cancellationToken)).SingleOrDefault()
        ?? throw CatalogErrors.SpaceNotFound();

    public async Task<SpaceDto> CreateAsync(SpaceRequest request, CancellationToken cancellationToken)
    {
        await EnsureLocationExistsAsync(request.LocationId, cancellationToken);

        var space = new Space(request.ToDetails(), timeProvider.GetUtcNow());
        db.Spaces.Add(space);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Создан коворкинг {SpaceId} «{Name}»", space.Id, space.Name);
        return await GetByIdAsync(space.Id, new SpaceAvailabilityQuery(), cancellationToken);
    }

    public async Task<SpaceDto> UpdateAsync(int id, SpaceRequest request, CancellationToken cancellationToken)
    {
        var space = await db.Spaces.FindAsync([id], cancellationToken) ?? throw CatalogErrors.SpaceNotFound();
        await EnsureLocationExistsAsync(request.LocationId, cancellationToken);

        // Схему нельзя уменьшить так, чтобы какое-то место оказалось за её краем.
        if (await db.Resources.AnyAsync(r => r.SpaceId == id && (r.SeatRow > request.Rows || r.SeatColumn > request.Columns), cancellationToken))
        {
            throw CatalogErrors.SeatOutsideLayout(request.Rows, request.Columns);
        }

        space.Update(request.ToDetails(), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Изменён коворкинг {SpaceId}", id);
        return await GetByIdAsync(id, new SpaceAvailabilityQuery(), cancellationToken);
    }

    /// <summary>Удалить можно только пустой коворкинг: места ссылаются на него внешним ключом Restrict (409 <c>in_use</c>).</summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var space = await db.Spaces.FindAsync([id], cancellationToken) ?? throw CatalogErrors.SpaceNotFound();

        db.Spaces.Remove(space);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Удалён коворкинг {SpaceId}", id);
    }

    private async Task<IReadOnlyList<SpaceDto>> BuildAsync(IQueryable<Space> spaces, SpaceAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var spaceList = await spaces
            .Include(s => s.Location)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var spaceIds = spaceList.Select(s => s.Id).ToArray();

        // Места — обычные ресурсы. Целиком, а не проекцией: правилам BookingRules нужен сам ресурс.
        var seats = await db.Resources
            .AsNoTracking()
            .Include(r => r.Features)
            .Where(r => r.SpaceId != null && spaceIds.Contains(r.SpaceId.Value))
            .OrderBy(r => r.SeatNumber)
            .ToListAsync(cancellationToken);

        var window = query.From is { } f && query.To is { } t ? (From: f.ToUniversalTime(), To: t.ToUniversalTime()) : ((DateTimeOffset From, DateTimeOffset To)?)null;
        var states = window is { } w
            ? await SeatStatesAsync(seats, w.From, w.To, cancellationToken)
            : seats.ToDictionary(s => s.Id, _ => ((SeatStatus?)null, (DateTimeOffset?)null));

        return spaceList.Select(space =>
        {
            var own = seats.Where(s => s.SpaceId == space.Id).ToList();
            var active = own.Where(s => s.IsActive).ToList();

            var seatDtos = own.Select(s => new SeatDto(
                s.Id,
                s.SeatNumber!.Value,
                s.SeatRow!.Value,
                s.SeatColumn!.Value,
                s.Features.Select(x => x.Code).Order().ToList(),
                s.IsActive,
                states[s.Id].Item1,
                states[s.Id].Item2)).ToList();

            return new SpaceDto(
                space.Id,
                space.Name,
                space.Description,
                new LocationDto(space.Location.Id, space.Location.Name, space.Location.Address),
                space.Floor,
                space.IsQuiet,
                space.Rows,
                space.Columns,
                space.AisleAfterColumn,
                active.Count > 0 ? active.Min(s => s.OpenTime) : null,
                active.Count > 0 ? active.Max(s => s.CloseTime) : null,
                active.Count,
                window is null ? null : seatDtos.Count(s => s.Status == SeatStatus.Free),
                seatDtos);
        }).ToList();
    }

    /// <summary>Состояние каждого места на интервал и, у свободных, до какого момента они свободны.</summary>
    private async Task<Dictionary<int, (SeatStatus?, DateTimeOffset?)>> SeatStatesAsync(
        IReadOnlyCollection<Resource> seats, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var seatIds = seats.Select(s => s.Id).ToArray();
        var dayEnd = policy.AtLocal(policy.ToLocal(to).Date.AddDays(1), TimeOnly.MinValue);

        // Активные брони мест, которые пересекают интервал или начинаются после него до конца того же дня.
        var bookings = await db.Bookings
            .AsNoTracking()
            .Where(b => seatIds.Contains(b.ResourceId)
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && b.StartAt < dayEnd && from < b.EndAt)
            .Select(b => new { b.ResourceId, b.UserId, b.StartAt, b.EndAt })
            .ToListAsync(cancellationToken);

        int? userId = currentUser.IsAuthenticated ? currentUser.Id : null;

        return seats.ToDictionary(seat => seat.Id, (Resource seat) =>
        {
            var own = bookings.Where(b => b.ResourceId == seat.Id).ToList();
            var overlapping = own.Where(b => BookingRules.Overlaps(from, to, b.StartAt, b.EndAt)).ToList();

            if (overlapping.Count > 0)
            {
                return ((SeatStatus?)(overlapping.Any(b => b.UserId == userId) ? SeatStatus.Mine : SeatStatus.Busy), (DateTimeOffset?)null);
            }

            var bookable = (BookingRules.CheckResourceActive(seat)
                            ?? BookingRules.CheckDuration(from, to, seat, policy)
                            ?? BookingRules.CheckWorkingHours(from, to, seat, policy)) is null;
            if (!bookable)
            {
                return ((SeatStatus?)SeatStatus.Unavailable, (DateTimeOffset?)null);
            }

            var close = policy.AtLocal(policy.ToLocal(to).Date, seat.CloseTime);
            var next = own.Where(b => b.StartAt >= to).Select(b => (DateTimeOffset?)b.StartAt).Min();
            return ((SeatStatus?)SeatStatus.Free, next is { } n && n < close ? n : close);
        });
    }

    private async Task EnsureLocationExistsAsync(int locationId, CancellationToken cancellationToken)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, cancellationToken))
        {
            throw CommonErrors.ReferenceNotFound($"Корпус с id {locationId} не найден.");
        }
    }
}
