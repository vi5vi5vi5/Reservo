using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Bookings;

/// <summary>Почему предложен вариант.</summary>
public enum SuggestionReason
{
    /// <summary>Тот же ресурс, раньше на ту же длительность.</summary>
    Earlier,

    /// <summary>Тот же ресурс, позже на ту же длительность.</summary>
    Later,

    /// <summary>Похожий ресурс (та же категория, подходит по вместимости) на то же время.</summary>
    SimilarResource,
}

/// <summary>Свободный вариант вместо занятого времени.</summary>
/// <param name="ResourceId">Id ресурса.</param>
/// <param name="ResourceName">Название ресурса.</param>
/// <param name="LocationName">Корпус.</param>
/// <param name="StartAt">Начало (UTC).</param>
/// <param name="EndAt">Конец (UTC).</param>
/// <param name="Reason">Почему предложен: <c>Earlier</c>, <c>Later</c> или <c>SimilarResource</c>.</param>
public sealed record BookingSuggestionDto(
    int ResourceId, string ResourceName, string LocationName, DateTimeOffset StartAt, DateTimeOffset EndAt, SuggestionReason Reason);

/// <summary>Желаемая бронь, для которой ищутся варианты.</summary>
public sealed record SuggestionQuery
{
    /// <summary>Желаемое начало, со смещением.</summary>
    public DateTimeOffset? StartAt { get; init; }

    /// <summary>Желаемый конец, со смещением.</summary>
    public DateTimeOffset? EndAt { get; init; }

    /// <summary>Число участников — похожий ресурс должен их вместить.</summary>
    public int? Attendees { get; init; }
}

public sealed class SuggestionQueryValidator : AbstractValidator<SuggestionQuery>
{
    public const int MaxDays = 7;

    public SuggestionQueryValidator()
    {
        RuleFor(x => x.StartAt).NotNull().WithMessage("Укажите желаемое начало (startAt).");
        RuleFor(x => x.EndAt).NotNull().WithMessage("Укажите желаемый конец (endAt).");

        RuleFor(x => x.EndAt)
            .Must((query, end) => end > query.StartAt).WithMessage("Конец должен быть позже начала.")
            .Must((query, end) => end - query.StartAt <= TimeSpan.FromDays(MaxDays)).WithMessage($"Интервал не длиннее {MaxDays} дней.")
            .When(x => x.StartAt is not null && x.EndAt is not null);

        RuleFor(x => x.Attendees).GreaterThan(0).WithMessage("Число участников должно быть больше нуля.");
    }
}

public interface IBookingSuggestionService
{
    Task<IReadOnlyList<BookingSuggestionDto>> GetSuggestionsAsync(int resourceId, SuggestionQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// «Время заняли — вот варианты рядом» (UC-24). Фронтенд вызывает это после ответа 409 <c>booking.time_conflict</c>.
/// Каждый вариант проходит те же правила, что и настоящая бронь (BookingRules.CheckSlot) и свободен на момент запроса.
/// Гарантии, что его не займут через секунду, нет — её даёт только сама бронь.
/// </summary>
public sealed class BookingSuggestionService(IAppDbContext db, BookingPolicy policy, TimeProvider timeProvider) : IBookingSuggestionService
{
    private const int MaxSimilar = 2;

    public async Task<IReadOnlyList<BookingSuggestionDto>> GetSuggestionsAsync(
        int resourceId, SuggestionQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var startAt = query.StartAt!.Value.ToUniversalTime();
        var endAt = query.EndAt!.Value.ToUniversalTime();
        var duration = endAt - startAt;

        var resource = await db.Resources
            .AsNoTracking()
            .Include(r => r.Location)
            .SingleOrDefaultAsync(r => r.Id == resourceId, cancellationToken)
            ?? throw CommonErrors.NotFound("Ресурс не найден.");

        var suggestions = new List<BookingSuggestionDto>();

        // Тот же ресурс: ближайшее свободное начало раньше и позже в тот же местный день, с шагом сетки.
        var day = policy.ToLocal(startAt).Date;
        var dayStart = policy.AtLocal(day, TimeOnly.MinValue);
        var dayEnd = policy.AtLocal(day.AddDays(1), TimeOnly.MinValue);

        var busy = await ActiveBookings()
            .Where(b => b.ResourceId == resourceId && b.StartAt < dayEnd + duration && dayStart < b.EndAt)
            .Select(b => new { b.StartAt, b.EndAt })
            .ToListAsync(cancellationToken);

        bool Fits(DateTimeOffset s) =>
            BookingRules.CheckSlot(s, s + duration, query.Attendees, resource, policy, now) is null
            && !busy.Any(b => BookingRules.Overlaps(s, s + duration, b.StartAt, b.EndAt));

        var step = TimeSpan.FromMinutes(policy.SlotStepMinutes);
        var earlier = Search(startAt - step, s => s >= dayStart, -step, Fits);
        var later = Search(startAt + step, s => s < dayEnd, step, Fits);

        if (earlier is { } e)
        {
            suggestions.Add(new(resource.Id, resource.Name, resource.Location.Name, e, e + duration, SuggestionReason.Earlier));
        }

        if (later is { } l)
        {
            suggestions.Add(new(resource.Id, resource.Name, resource.Location.Name, l, l + duration, SuggestionReason.Later));
        }

        // Ближние по времени варианты — первыми.
        suggestions.Sort((a, b) => (a.StartAt - startAt).Duration().CompareTo((b.StartAt - startAt).Duration()));

        // Похожий ресурс на то же время: та же категория, вмещает всех, свободен. Сначала тот же коворкинг, потом тот же корпус.
        var attendees = query.Attendees;
        var candidates = await db.Resources
            .AsNoTracking()
            .Include(r => r.Location)
            .Where(r => r.CategoryId == resource.CategoryId && r.Id != resource.Id && r.IsActive
                        && (attendees == null || r.Capacity == null || r.Capacity >= attendees)
                        && !db.Bookings.Any(b => b.ResourceId == r.Id
                                                 && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                                                 && b.StartAt < endAt && startAt < b.EndAt))
            .OrderByDescending(r => resource.SpaceId != null && r.SpaceId == resource.SpaceId)
            .ThenByDescending(r => r.LocationId == resource.LocationId)
            .ThenBy(r => r.SeatNumber)
            .ThenBy(r => r.Name)
            .Take(20)
            .ToListAsync(cancellationToken);

        suggestions.AddRange(candidates
            .Where(r => BookingRules.CheckSlot(startAt, endAt, attendees, r, policy, now) is null)
            .Take(MaxSimilar)
            .Select(r => new BookingSuggestionDto(r.Id, r.Name, r.Location.Name, startAt, endAt, SuggestionReason.SimilarResource)));

        return suggestions;
    }

    /// <summary>Перебирает начала с шагом сетки, пока не найдёт подходящее или не выйдет за границу дня.</summary>
    private static DateTimeOffset? Search(
        DateTimeOffset first, Func<DateTimeOffset, bool> withinDay, TimeSpan step, Func<DateTimeOffset, bool> fits)
    {
        for (var s = first; withinDay(s); s += step)
        {
            if (fits(s))
            {
                return s;
            }
        }

        return null;
    }

    private IQueryable<Booking> ActiveBookings() =>
        db.Bookings.AsNoTracking().Where(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);
}
