using System.Net;
using System.Net.Http.Json;
using Reservo.Application.Admin;
using Reservo.Application.Audit;
using Reservo.Application.Bookings;
using Reservo.Application.Catalog.Resources;
using Reservo.Application.Catalog.Spaces;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;
using Reservo.IntegrationTests.Infrastructure;

namespace Reservo.IntegrationTests;

/// <summary>Возможности, которые появились под мобильный дизайн: коворкинги, брони на ночь, подсказки, дашборд.</summary>
public class DesignFeaturesTests(ReservoApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Seat_map_shows_free_busy_and_own_seats()
    {
        var admin = await _api.LoginAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var (space, seats) = await CreateSpaceWithSeatsAsync(admin, marker, seatCount: 3);
        var user = await _api.RegisterAsync();
        var from = Moscow.At(3, 10);
        var to = Moscow.At(3, 12);
        var window = $"from={TestApi.Q(from)}&to={TestApi.Q(to)}";

        var empty = await _api.Anonymous().GetFromJsonAsync<SpaceDto>($"/api/spaces/{space.Id}?{window}", TestApi.Json);
        Assert.Equal(3, empty!.FreeSeats);
        Assert.All(empty.Seats, s => Assert.Equal(SeatStatus.Free, s.Status));

        // Место бронируется как обычный ресурс, и тема для места необязательна.
        var response = await user.Client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            ResourceId = seats[0].Id,
            StartAt = from,
            EndAt = to,
        }, TestApi.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await TestApi.ReadAsync<BookingDto>(response);
        Assert.Null(booking.Title);
        Assert.Equal(ResourceKind.Seat, booking.Resource.Kind);
        Assert.Equal(space.Name, booking.Resource.SpaceName);
        Assert.Equal(1, booking.Resource.SeatNumber);

        var mine = await user.Client.GetFromJsonAsync<SpaceDto>($"/api/spaces/{space.Id}?{window}", TestApi.Json);
        Assert.Equal(SeatStatus.Mine, mine!.Seats.Single(s => s.Number == 1).Status);
        Assert.Equal(2, mine.FreeSeats);

        var stranger = await _api.Anonymous().GetFromJsonAsync<SpaceDto>($"/api/spaces/{space.Id}?{window}", TestApi.Json);
        Assert.Equal(SeatStatus.Busy, stranger!.Seats.Single(s => s.Number == 1).Status);

        // Свободное место свободно до закрытия коворкинга (22:00), а схема знает, у кого компьютер.
        var free = stranger.Seats.Single(s => s.Number == 2);
        Assert.Equal(Moscow.At(3, 22), free.FreeUntil);
        Assert.Contains("computers", stranger.Seats.Single(s => s.Number == 1).FeatureCodes);

        // В общем каталоге мест нет, а с фильтром по коворкингу — есть.
        var catalog = await _api.Anonymous().GetFromJsonAsync<PagedResult<ResourceDto>>($"/api/resources?search={marker}", TestApi.Json);
        Assert.Empty(catalog!.Items);
        var bySpace = await _api.Anonymous().GetFromJsonAsync<PagedResult<ResourceDto>>($"/api/resources?spaceId={space.Id}", TestApi.Json);
        Assert.Equal(3, bySpace!.TotalCount);
        Assert.All(bySpace.Items, r => Assert.Equal(space.Id, r.Seat!.SpaceId));
    }

    [Fact]
    public async Task Seat_placement_is_checked()
    {
        var admin = await _api.LoginAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var (space, seats) = await CreateSpaceWithSeatsAsync(admin, marker, seatCount: 1);
        var category = seats[0].Category;

        ResourceRequest Seat(int number, int row, int column) => new()
        {
            CategoryId = category.Id,
            LocationId = space.Location.Id,
            Name = $"Место {marker}-{number}",
            OpenTime = new TimeOnly(8, 0),
            CloseTime = new TimeOnly(22, 0),
            SpaceId = space.Id,
            SeatNumber = number,
            SeatRow = row,
            SeatColumn = column,
        };

        var outside = await admin.Client.PostAsJsonAsync("/api/resources", Seat(10, 1, 99), TestApi.Json);
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);

        var duplicate = await admin.Client.PostAsJsonAsync("/api/resources", Seat(1, 2, 2), TestApi.Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("catalog.duplicate", await TestApi.ErrorCodeAsync(duplicate));

        // Помещению положение на схеме задавать нельзя, а месту без него — нельзя быть.
        var roomWithSeat = await admin.Client.PostAsJsonAsync("/api/resources", Seat(5, 2, 2) with
        {
            CategoryId = (await TestApi.CreateCategoryAsync(admin, ResourceKind.Room)).Id,
        }, TestApi.Json);
        Assert.Equal(HttpStatusCode.BadRequest, roomWithSeat.StatusCode);

        var seatWithoutPlace = await admin.Client.PostAsJsonAsync("/api/resources", Seat(6, 2, 2) with
        {
            SpaceId = null, SeatNumber = null, SeatRow = null, SeatColumn = null,
        }, TestApi.Json);
        Assert.Equal(HttpStatusCode.BadRequest, seatWithoutPlace.StatusCode);
    }

    [Fact]
    public async Task Overnight_equipment_can_be_booked_for_days_without_blocking_rooms()
    {
        var admin = await _api.LoginAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var equipment = await TestApi.CreateCategoryAsync(admin, ResourceKind.Equipment);
        var laptop = await _api.CreateResourceAsync(admin, name: $"Ноутбук {marker}", customize: r => r with
        {
            CategoryId = equipment.Id,
            Capacity = null,
            AllowsOvernight = true,
            MaxDurationMinutes = 3 * 24 * 60,
            PickupInfo = "Стойка информации",
        });
        var room = await _api.CreateResourceAsync(admin, name: $"Переговорная {marker}");
        var user = await _api.RegisterAsync();

        // Ноутбук на ночь: вечером взял, утром вернул.
        var laptopBooking = await TestApi.BookAsync(user, laptop.Id, Moscow.At(7, 18), Moscow.At(8, 9));
        Assert.Equal(HttpStatusCode.Created, laptopBooking.StatusCode);
        Assert.True((await TestApi.ReadAsync<BookingDto>(laptopBooking)).Resource.AllowsOvernight);

        // Оборудование не занимает самого человека (BR-11): в это же время можно сидеть в переговорной.
        var roomBooking = await TestApi.BookAsync(user, room.Id, Moscow.At(7, 19), Moscow.At(7, 20));
        Assert.Equal(HttpStatusCode.Created, roomBooking.StatusCode);

        // А вот две переговорные одновременно — нельзя.
        // Лимит длительности — сутки, чтобы ночную бронь остановило именно правило рабочих часов.
        var otherRoom = await _api.CreateResourceAsync(admin, name: $"Вторая {marker}", customize: r => r with { MaxDurationMinutes = 24 * 60 });
        var overlap = await TestApi.BookAsync(user, otherRoom.Id, Moscow.At(7, 19, 30), Moscow.At(7, 20, 30));
        Assert.Equal("booking.user_overlap", await TestApi.ErrorCodeAsync(overlap));

        // Обычный ресурс через полночь не бронируется.
        var overMidnight = await TestApi.BookAsync(user, otherRoom.Id, Moscow.At(9, 20), Moscow.At(10, 9));
        Assert.Equal("booking.outside_working_hours", await TestApi.ErrorCodeAsync(overMidnight));

        // Поиск «на 2 дня» находит только то, что бронируется на несколько дней.
        var page = await _api.Anonymous().GetFromJsonAsync<PagedResult<AvailableResourceDto>>(
            $"/api/resources/available?search={marker}&from={TestApi.Q(Moscow.At(10, 10))}&to={TestApi.Q(Moscow.At(12, 10))}", TestApi.Json);
        Assert.Equal([laptop.Id], page!.Items.Select(i => i.Resource.Id));
        Assert.Null(page.Items[0].FreeUntil);
    }

    [Fact]
    public async Task Title_is_required_for_rooms()
    {
        var admin = await _api.LoginAdminAsync();
        var room = await _api.CreateResourceAsync(admin);
        var user = await _api.RegisterAsync();

        var response = await TestApi.BookAsync(user, room.Id, Moscow.At(5, 10), Moscow.At(5, 11), title: " ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await TestApi.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Suggestions_offer_nearest_free_time_and_similar_resource()
    {
        var admin = await _api.LoginAdminAsync();
        var category = await TestApi.CreateCategoryAsync(admin, ResourceKind.Room);
        var busy = await _api.CreateResourceAsync(admin, customize: r => r with { CategoryId = category.Id });
        var similar = await _api.CreateResourceAsync(admin, customize: r => r with { CategoryId = category.Id });
        var owner = await _api.RegisterAsync();

        await TestApi.BookOkAsync(owner, busy.Id, Moscow.At(5, 12), Moscow.At(5, 13));

        var suggestions = await _api.Anonymous().GetFromJsonAsync<List<BookingSuggestionDto>>(
            $"/api/resources/{busy.Id}/suggestions?startAt={TestApi.Q(Moscow.At(5, 12, 30))}&endAt={TestApi.Q(Moscow.At(5, 13, 30))}&attendees=4",
            TestApi.Json);

        Assert.Contains(suggestions!, s => s.Reason == SuggestionReason.Later && s.StartAt == Moscow.At(5, 13));
        Assert.Contains(suggestions!, s => s.Reason == SuggestionReason.Earlier && s.EndAt == Moscow.At(5, 12));
        Assert.Contains(suggestions!, s => s.Reason == SuggestionReason.SimilarResource && s.ResourceId == similar.Id);
    }

    [Fact]
    public async Task Lists_support_several_statuses_search_and_entity_names()
    {
        var admin = await _api.LoginAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var room = await _api.CreateResourceAsync(admin, name: $"Поиск броней {marker}");
        var user = await _api.RegisterAsync();
        var cancelled = await TestApi.BookOkAsync(user, room.Id, Moscow.At(6, 9), Moscow.At(6, 10));
        var active = await TestApi.BookOkAsync(user, room.Id, Moscow.At(6, 11), Moscow.At(6, 12));
        (await user.Client.PostAsync($"/api/bookings/{cancelled.Id}/cancel", null)).EnsureSuccessStatusCode();

        // Вкладка «Отменённые» — два статуса сразу.
        var gone = await user.Client.GetFromJsonAsync<PagedResult<BookingDto>>("/api/bookings/my?status=Cancelled&status=Rejected", TestApi.Json);
        Assert.Equal([cancelled.Id], gone!.Items.Select(b => b.Id));

        var upcoming = await user.Client.GetFromJsonAsync<PagedResult<BookingDto>>(
            $"/api/bookings/my?status=Pending&status=Confirmed&from={TestApi.Q(DateTimeOffset.UtcNow)}", TestApi.Json);
        Assert.Equal([active.Id], upcoming!.Items.Select(b => b.Id));

        // Поиск админа по названию ресурса.
        var found = await admin.Client.GetFromJsonAsync<PagedResult<BookingDto>>($"/api/admin/bookings?search={marker}", TestApi.Json);
        Assert.Equal(2, found!.TotalCount);

        // В журнале у записи о брони есть название ресурса.
        var log = await admin.Client.GetFromJsonAsync<PagedResult<AuditLogDto>>(
            $"/api/admin/audit?entityType=Booking&entityId={active.Id}", TestApi.Json);
        Assert.All(log!.Items, entry => Assert.Equal(room.Name, entry.EntityName));
    }

    [Fact]
    public async Task Deactivation_reason_is_shown_on_resource_card()
    {
        var admin = await _api.LoginAdminAsync();
        var room = await _api.CreateResourceAsync(admin);

        (await admin.Client.PostAsJsonAsync($"/api/resources/{room.Id}/deactivate", new { reason = "Ремонт до конца недели" }, TestApi.Json))
            .EnsureSuccessStatusCode();

        var card = await _api.Anonymous().GetFromJsonAsync<ResourceDto>($"/api/resources/{room.Id}", TestApi.Json);
        Assert.False(card!.IsActive);
        Assert.Equal("Ремонт до конца недели", card.DeactivationReason);

        (await admin.Client.PostAsync($"/api/resources/{room.Id}/activate", null)).EnsureSuccessStatusCode();
        var back = await _api.Anonymous().GetFromJsonAsync<ResourceDto>($"/api/resources/{room.Id}", TestApi.Json);
        Assert.Null(back!.DeactivationReason);
    }

    [Fact]
    public async Task Admin_stats_show_load_by_hour()
    {
        var admin = await _api.LoginAdminAsync();
        var room = await _api.CreateResourceAsync(admin);
        var user = await _api.RegisterAsync();
        await TestApi.BookOkAsync(user, room.Id, Moscow.At(8, 10), Moscow.At(8, 11, 30));

        var date = DateOnly.FromDateTime(Moscow.At(8, 0).Date);
        var stats = await admin.Client.GetFromJsonAsync<AdminStatsDto>($"/api/admin/stats?date={date:yyyy-MM-dd}", TestApi.Json);

        var row = stats!.Heatmap.Single(r => r.Type == "resource" && r.Id == room.Id);
        Assert.Equal(100, row.BusyPercent[stats.Hours.ToList().IndexOf(10)]);
        Assert.Equal(50, row.BusyPercent[stats.Hours.ToList().IndexOf(11)]);
        Assert.Equal(0, row.BusyPercent[stats.Hours.ToList().IndexOf(12)]);
        Assert.True(stats.BookingsOnDate >= 1);

        // Обычному пользователю дашборд недоступен.
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync("/api/admin/stats")).StatusCode);
    }

    /// <summary>Свой коворкинг 2×3 со своей категорией мест; первый ряд — с компьютерами.</summary>
    private async Task<(SpaceDto Space, List<ResourceDto> Seats)> CreateSpaceWithSeatsAsync(Session admin, string marker, int seatCount)
    {
        var category = await TestApi.CreateCategoryAsync(admin, ResourceKind.Seat);
        var location = (await admin.Client.GetFromJsonAsync<List<Application.Catalog.Locations.LocationDto>>("/api/locations", TestApi.Json))!.First();
        var computers = (await admin.Client.GetFromJsonAsync<List<Application.Catalog.Features.FeatureDto>>("/api/features", TestApi.Json))!
            .Single(f => f.Code == "computers");

        var spaceResponse = await admin.Client.PostAsJsonAsync("/api/spaces", new SpaceRequest
        {
            LocationId = location.Id,
            Name = $"Коворкинг {marker}",
            Rows = 2,
            Columns = 3,
        }, TestApi.Json);
        Assert.Equal(HttpStatusCode.Created, spaceResponse.StatusCode);
        var space = await TestApi.ReadAsync<SpaceDto>(spaceResponse);

        var seats = new List<ResourceDto>();
        for (var number = 1; number <= seatCount; number++)
        {
            var row = (number - 1) / 3 + 1;
            var seat = await _api.CreateResourceAsync(admin, name: $"Место {marker}-{number}", customize: r => r with
            {
                CategoryId = category.Id,
                LocationId = location.Id,
                Capacity = null,
                CloseTime = new TimeOnly(22, 0),
                MaxDurationMinutes = 480,
                SpaceId = space.Id,
                SeatNumber = number,
                SeatRow = row,
                SeatColumn = (number - 1) % 3 + 1,
                FeatureIds = row == 1 ? [computers.Id] : [],
            });
            seats.Add(seat);
        }

        return (space, seats);
    }
}
