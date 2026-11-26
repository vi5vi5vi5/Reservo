using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;
using static Reservo.Infrastructure.Persistence.Seed.LargeCatalogData;

namespace Reservo.Infrastructure.Persistence.Seed;

/// <summary>
/// Большой демо-каталог «на 10 кампусов и 10 офисов»: тысячи помещений, мест и единиц оборудования
/// плюс немного хаотичных броней, чтобы расписания и дашборд выглядели живыми.
/// </summary>
/// <remarks>
/// Включается настройкой Seed:LargeCatalog (в Docker — SEED_LARGE_CATALOG=true) и ДОПОЛНЯЕТ обычный демо-каталог,
/// а не заменяет его: «Байкал», «Онега» и сценарий защиты остаются на месте. Добавляется один раз — признак
/// того, что каталог уже есть, — корпус первого кампуса. Поэтому на работающем сервере базу сносить не нужно.
///
/// Случайность детерминированная (фиксированное зерно): при каждом развёртывании получается тот же самый каталог.
/// Пользователей генератор не создаёт: брони принадлежат демо-пользователям teacher@ и student2@, а у student@ и
/// администратора своих броней почти нет — им ничто не мешает бронировать на показе (лимит активных броней — 5).
/// </remarks>
internal static class LargeCatalogSeeder
{
    private const int RandomSeed = 2026;
    private const int SlotMinutes = 15;
    private const int PastDays = 6;
    private const int FutureDays = 10;
    private const int MaxPending = 30;

    private static readonly string Marker = $"«{Campuses[0].Name}», корпус {BuildingLetters[0]}";

    private sealed record Place(Location Location, bool IsOffice, string Short, TimeOnly Open, TimeOnly Close);

    public static async Task SeedAsync(
        AppDbContext db, BookingPolicy policy, TimeProvider timeProvider, SeedOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        if (!options.LargeCatalog || await db.Locations.AnyAsync(l => l.Name == Marker, cancellationToken))
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var counts = (Resources: 0, Seats: 0, Spaces: 0, Bookings: 0);

        // Одна транзакция через стратегию повторов: либо каталог добавится целиком, либо не добавится вовсе.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            counts = await GenerateAsync(db, policy, timeProvider.GetUtcNow(), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        db.ChangeTracker.Clear();

        logger.LogInformation(
            "Добавлен большой демо-каталог за {Seconds:F1} с: {Resources} помещений и единиц оборудования, {Spaces} коворкингов с {Seats} местами, {Bookings} броней",
            watch.Elapsed.TotalSeconds, counts.Resources, counts.Spaces, counts.Seats, counts.Bookings);
    }

    private static async Task<(int Resources, int Seats, int Spaces, int Bookings)> GenerateAsync(
        AppDbContext db, BookingPolicy policy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rnd = new Random(RandomSeed);

        // ---------- Справочники: существующие берём по коду, недостающие добавляем ----------
        var categories = await db.Categories.ToDictionaryAsync(c => c.Code, cancellationToken);
        if (!categories.ContainsKey("coworking"))
        {
            categories["coworking"] = db.Categories.Add(new Category("coworking", "Места в коворкинге", "Рабочие места в открытых пространствах", ResourceKind.Seat)).Entity;
        }

        foreach (var k in RoomKinds.Where(k => !categories.ContainsKey(k.Code)))
        {
            categories[k.Code] = db.Categories.Add(new Category(k.Code, k.Category, k.Description, ResourceKind.Room)).Entity;
        }

        foreach (var e in Equipment.Where(e => !categories.ContainsKey(e.Code)))
        {
            categories[e.Code] = db.Categories.Add(new Category(e.Code, e.Category, null, ResourceKind.Equipment)).Entity;
        }

        var features = await db.Features.ToDictionaryAsync(f => f.Code, cancellationToken);
        (string Code, string Name)[] neededFeatures = [.. Features, ("window", "У окна"), ("computers", "Компьютеры")];
        foreach (var (code, name) in neededFeatures.Where(f => !features.ContainsKey(f.Code)))
        {
            features[code] = db.Features.Add(new Feature(code, name)).Entity;
        }

        // ---------- Корпуса кампусов и офисы ----------
        var places = new List<Place>();
        foreach (var c in Campuses)
        {
            for (var b = 0; b < c.Buildings; b++)
            {
                var location = new Location($"«{c.Name}», корпус {BuildingLetters[b]}", $"{c.Street}, {rnd.Next(1, 90)}, стр. {b + 1}");
                places.Add(new Place(location, false, c.Name, new TimeOnly(8, 0), new TimeOnly(21, 0)));
            }
        }

        foreach (var (name, address) in Offices)
        {
            places.Add(new Place(new Location(name, address), true, name.Replace("Офис ", ""), new TimeOnly(8, 0), new TimeOnly(22, 0)));
        }

        // Одно сохранение выдаёт id новым категориям, оснащению и корпусам — они нужны ресурсам.
        db.Locations.AddRange(places.Select(p => p.Location));
        await db.SaveChangesAsync(cancellationToken);

        // ---------- Помещения и оборудование ----------
        var resources = new List<Resource>();
        var inventory = new Dictionary<string, int>();
        var meetingNames = Shuffle(MeetingNames, rnd);
        var meetingIndex = 0;
        var labIndex = 0;

        foreach (var place in places)
        {
            var plan = place.IsOffice ? OfficeRooms : CampusRooms;
            foreach (var (code, (min, max)) in plan)
            {
                var kind = RoomKinds.First(k => k.Code == code);
                var count = rnd.Next(min, max + 1);
                for (var i = 1; i <= count; i++)
                {
                    var floor = place.IsOffice ? rnd.Next(3, 26) : rnd.Next(1, 7);
                    var name = code switch
                    {
                        "meeting-room" => $"Переговорная «{meetingNames[meetingIndex++ % meetingNames.Length]}»",
                        "classroom" => $"Аудитория {Letter(place)}-{floor}{rnd.Next(1, 40):00}",
                        "lab" => $"Лаборатория {LabNames[labIndex++ % LabNames.Length]}",
                        _ => count > 1 ? $"{kind.Singular} {i}" : kind.Singular,
                    };
                    var (open, close) = RoomHours(code, place);
                    var capacity = Round(rnd.Next(kind.MinCapacity, kind.MaxCapacity + 1));
                    var resource = new Resource(
                        new ResourceDetails(
                            categories[code].Id,
                            place.Location.Id,
                            name,
                            RoomDescription(kind, place, capacity, rnd),
                            floor,
                            capacity,
                            ImageUrl: null,
                            RequiresApproval: rnd.NextDouble() < kind.ApprovalChance,
                            open,
                            close,
                            kind.MaxMinutes),
                        now);
                    resource.SetFeatures(kind.Features.Where(_ => rnd.NextDouble() < 0.5).Select(f => features[f]));
                    resources.Add(resource);
                }
            }

            // Оборудование: в кампусе — почти любое, в офисе — офисное.
            foreach (var e in Equipment)
            {
                var chance = place.IsOffice ? (e.Office ? 0.6 : 0.04) : 0.45;
                if (rnd.NextDouble() >= chance)
                {
                    continue;
                }

                var count = rnd.Next(e.MinCount, e.MaxCount + 1);
                for (var i = 0; i < count; i++)
                {
                    var number = inventory[e.Code] = inventory.GetValueOrDefault(e.Code, 100) + rnd.Next(1, 4);
                    var model = e.Models.Length > 0 ? $" {e.Models[rnd.Next(e.Models.Length)]}" : "";
                    var (open, close) = place.IsOffice ? (new TimeOnly(9, 0), new TimeOnly(20, 0)) : (place.Open, place.Close);
                    var resource = new Resource(
                        new ResourceDetails(
                            categories[e.Code].Id,
                            place.Location.Id,
                            $"{e.Singular}{model} №{number}",
                            $"{e.Singular}{model} из фонда {(place.IsOffice ? "офиса" : "кампуса")} «{place.Short.Trim('«', '»')}». Проверьте комплектность при получении.",
                            1,
                            Capacity: null,
                            ImageUrl: null,
                            e.Approval,
                            open,
                            close,
                            e.MaxMinutes,
                            e.Overnight,
                            $"Выдача и возврат — стойка информации, {place.Location.Name}, 1 этаж, {open:HH\\:mm}–{close:HH\\:mm}."),
                        now);
                    resources.Add(resource);
                }
            }
        }

        db.Resources.AddRange(resources);

        // ---------- Коворкинги: по два в кампусе, опенспейс в каждом офисе ----------
        var spaceSeeds = new List<(Space Space, Place Place, int PcRows)>();
        foreach (var group in places.GroupBy(p => p.IsOffice ? p.Location.Name : p.Short))
        {
            var list = group.ToList();
            var isOffice = list[0].IsOffice;
            var variants = isOffice
                ? new[] { ($"Опенспейс {list[0].Short}", false) }
                : new[] { ($"Коворкинг «{list[0].Short}»", false), ($"Тихий зал «{list[0].Short}»", true) };

            foreach (var (name, quiet) in variants)
            {
                var place = list[rnd.Next(list.Count)];
                var rows = rnd.Next(3, 7);
                var columns = rnd.Next(2, 5) * 2;
                var space = new Space(
                    new SpaceDetails(place.Location.Id, name,
                        quiet ? "Тихая зона: без звонков и разговоров." : "Открытое пространство для работы и встреч вполголоса.",
                        isOffice ? rnd.Next(3, 26) : rnd.Next(1, 5), quiet, rows, columns, columns >= 6 ? columns / 2 : null),
                    now);
                spaceSeeds.Add((space, place, rnd.Next(1, rows)));
            }
        }

        db.Spaces.AddRange(spaceSeeds.Select(s => s.Space));
        await db.SaveChangesAsync(cancellationToken);

        var seats = new List<Resource>();
        foreach (var (space, place, pcRows) in spaceSeeds)
        {
            var number = 1;
            for (var row = 1; row <= space.Rows; row++)
            {
                for (var column = 1; column <= space.Columns; column++, number++)
                {
                    var seat = new Resource(
                        new ResourceDetails(
                            categories["coworking"].Id,
                            place.Location.Id,
                            $"{space.Name}, место {number}",
                            space.Description,
                            space.Floor,
                            Capacity: null,
                            ImageUrl: null,
                            RequiresApproval: false,
                            space.IsQuiet ? new TimeOnly(9, 0) : new TimeOnly(8, 0),
                            space.IsQuiet ? new TimeOnly(21, 0) : new TimeOnly(22, 0),
                            480,
                            Seat: new SeatPlacement(space.Id, number, row, column)),
                        now);

                    var seatFeatures = new List<Feature>();
                    if (row <= pcRows)
                    {
                        seatFeatures.Add(features["computers"]);
                    }

                    if (row == 1)
                    {
                        seatFeatures.Add(features["window"]);
                    }

                    seat.SetFeatures(seatFeatures);
                    seats.Add(seat);
                }
            }
        }

        db.Resources.AddRange(seats);
        await db.SaveChangesAsync(cancellationToken);

        // ---------- Хаотичные брони — немного, чтобы было что бронировать ----------
        var bookings = await AddBookingsAsync(db, policy, now, resources, seats, rnd, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return (resources.Count, seats.Count, spaceSeeds.Count, bookings);
    }

    /// <summary>
    /// Брони на несколько дней назад и вперёд: у помещения — изредка, у оборудования — ещё реже, у мест — чуть чаще.
    /// Правила те же, что у настоящих броней: сетка 15 минут, рабочие часы, лимит длительности, без пересечений.
    /// </summary>
    private static async Task<int> AddBookingsAsync(
        AppDbContext db, BookingPolicy policy, DateTimeOffset now, List<Resource> resources, List<Resource> seats, Random rnd,
        CancellationToken cancellationToken)
    {
        string[] owners = ["teacher@reservo.example", "student2@reservo.example"];
        var userIds = await db.Users.Where(u => owners.Contains(u.Email)).Select(u => u.Id).ToListAsync(cancellationToken);
        if (userIds.Count == 0)
        {
            return 0;
        }

        var today = policy.ToLocal(now).Date;
        var taken = new Dictionary<int, List<(DateTimeOffset S, DateTimeOffset E)>>();
        var pending = 0;
        var added = 0;

        bool Free(int resourceId, DateTimeOffset s, DateTimeOffset e)
        {
            var list = taken.TryGetValue(resourceId, out var l) ? l : taken[resourceId] = [];
            if (list.Any(x => x.S < e && s < x.E))
            {
                return false;
            }

            list.Add((s, e));
            return true;
        }

        foreach (var r in resources.Concat(seats))
        {
            var isSeat = r.SpaceId != null;
            var isEquipment = r.Capacity == null && !isSeat;
            var chance = isSeat ? 0.05 : isEquipment ? 0.007 : 0.03;
            var open = r.OpenTime.Hour * 60 + r.OpenTime.Minute;
            var close = r.CloseTime.Hour * 60 + r.CloseTime.Minute;
            var max = r.MaxDurationMinutes ?? policy.DefaultMaxDurationMinutes;

            for (var day = -PastDays; day <= FutureDays; day++)
            {
                if (rnd.NextDouble() >= chance)
                {
                    continue;
                }

                var date = today.AddDays(day);
                var perDay = isEquipment ? 1 : rnd.Next(1, isSeat ? 3 : 4);
                var cursor = open + rnd.Next(0, 4) * 60;

                for (var n = 0; n < perDay; n++)
                {
                    DateTimeOffset start, end;
                    if (r.AllowsOvernight && rnd.NextDouble() < 0.35)
                    {
                        // На ночь или на несколько дней: выдача вечером, возврат утром через 1–3 дня.
                        var startMin = Math.Max(open, close - rnd.Next(1, 5) * 60);
                        var days = Math.Min(rnd.Next(1, 4), Math.Max(1, max / 1440));
                        start = policy.AtLocal(date, Minutes(startMin));
                        end = policy.AtLocal(date.AddDays(days), Minutes(open + rnd.Next(0, 3) * 60));
                        if ((end - start).TotalMinutes > max)
                        {
                            end = start.AddMinutes(max / SlotMinutes * SlotMinutes);
                        }
                    }
                    else
                    {
                        int[] options = isSeat ? [60, 90, 120, 180, 240] : isEquipment ? [120, 180, 240, 360, 480] : [45, 60, 90, 120, 180];
                        var length = Math.Min(options[rnd.Next(options.Length)], max);
                        var startMin = cursor + rnd.Next(0, 5) * SlotMinutes;
                        if (startMin + length > close)
                        {
                            break;
                        }

                        start = policy.AtLocal(date, Minutes(startMin));
                        end = start.AddMinutes(length);
                        cursor = startMin + length + rnd.Next(0, 3) * 30;
                    }

                    if (!Free(r.Id, start, end))
                    {
                        continue;
                    }

                    var created = Min(now, start.AddHours(-rnd.Next(2, 96)));
                    var title = isSeat && rnd.NextDouble() < 0.7 ? null : Topics[rnd.Next(Topics.Length)];
                    var attendees = r.Capacity is { } cap && rnd.NextDouble() < 0.6 ? rnd.Next(1, cap + 1) : (int?)null;
                    var booking = new Booking(r.Id, userIds[rnd.Next(userIds.Count)], start, end, title, attendees, r.RequiresApproval, created);

                    if (end <= now)
                    {
                        var roll = rnd.NextDouble();
                        if (roll < 0.1)
                        {
                            booking.Cancel("Планы изменились", created);
                        }
                        else if (r.RequiresApproval && roll < 0.18)
                        {
                            booking.Reject("Ресурс занят под плановые работы", created);
                        }
                        else
                        {
                            if (booking.Status == BookingStatus.Pending)
                            {
                                booking.Approve(created);
                            }

                            booking.Complete(end);
                        }
                    }
                    else if (start > now && rnd.NextDouble() < 0.06)
                    {
                        booking.Cancel("Планы изменились", created);
                    }
                    else if (booking.Status == BookingStatus.Pending && (start <= now || pending >= MaxPending || rnd.NextDouble() < 0.6))
                    {
                        // Заявок в очереди немного, а начавшаяся бронь не может ждать согласования.
                        booking.Approve(created);
                    }
                    else if (booking.Status == BookingStatus.Pending)
                    {
                        pending++;
                    }

                    db.Bookings.Add(booking);
                    added++;
                }
            }
        }

        return added;
    }

    private static (TimeOnly Open, TimeOnly Close) RoomHours(string code, Place place) => code switch
    {
        "lab" or "workshop" or "recording-studio" or "video-studio" or "photo-studio" => (new TimeOnly(9, 0), new TimeOnly(20, 0)),
        "gym" => (new TimeOnly(7, 0), new TimeOnly(22, 0)),
        "reading-room" => (new TimeOnly(8, 0), new TimeOnly(22, 0)),
        "assembly-hall" or "exhibition-space" => (new TimeOnly(10, 0), new TimeOnly(21, 0)),
        _ => (place.Open, place.Close),
    };

    private static string RoomDescription(RoomKind kind, Place place, int capacity, Random rnd)
    {
        string[] tails =
        [
            "Есть окна и естественный свет.", "Рядом кулер и гардероб.", "Хорошая акустика.", "Можно переставлять мебель.",
            "Удобно для работы в группах.", "Тихо даже в перерывах.", "Рядом лифт.", "Есть розетки вдоль стен.",
        ];
        var lead = kind.Description ?? kind.Category;
        return $"{lead}. До {capacity} человек, {(place.IsOffice ? "офис" : "кампус")} {place.Short.Trim('«', '»')}. {tails[rnd.Next(tails.Length)]}";
    }

    private static string Letter(Place place) =>
        place.Location.Name.Length > 0 ? place.Location.Name[^1].ToString() : "А";

    private static int Round(int capacity) => capacity >= 40 ? capacity / 10 * 10 : capacity;

    private static TimeOnly Minutes(int minutes) => new(Math.Min(23, minutes / 60), minutes % 60);

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static T[] Shuffle<T>(T[] source, Random rnd)
    {
        var copy = source.ToArray();
        rnd.Shuffle(copy);
        return copy;
    }
}
