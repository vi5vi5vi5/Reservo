using System.Net;
using System.Net.Http.Json;
using Reservo.Application.Audit;
using Reservo.Application.Bookings;
using Reservo.Application.Catalog.Resources;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Audit;
using Reservo.Domain.Bookings;
using Reservo.IntegrationTests.Infrastructure;

namespace Reservo.IntegrationTests;

public class BookingTests(ReservoApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Overlapping_booking_is_409_but_adjacent_one_is_fine()
    {
        var admin = await _api.LoginAdminAsync();
        var resource = await _api.CreateResourceAsync(admin);
        var first = await _api.RegisterAsync();
        var second = await _api.RegisterAsync();

        await TestApi.BookOkAsync(first, resource.Id, Moscow.At(1, 14), Moscow.At(1, 15));

        var overlap = await TestApi.BookAsync(second, resource.Id, Moscow.At(1, 14, 30), Moscow.At(1, 15, 30));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.Equal("booking.time_conflict", await TestApi.ErrorCodeAsync(overlap));

        // Интервалы полуоткрытые: 15:00 — конец первой брони и начало второй, пересечения нет.
        var adjacent = await TestApi.BookAsync(second, resource.Id, Moscow.At(1, 15), Moscow.At(1, 16));
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
    }

    [Fact]
    public async Task Ten_parallel_requests_for_one_slot_produce_exactly_one_booking()
    {
        var admin = await _api.LoginAdminAsync();
        var resource = await _api.CreateResourceAsync(admin);
        var users = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => _api.RegisterAsync()));

        // Все десять запросов уходят одновременно: проверку в коде могут пройти несколько,
        // но exclusion constraint в PostgreSQL пропустит только один.
        var responses = await Task.WhenAll(users.Select(user =>
            TestApi.BookAsync(user, resource.Id, Moscow.At(2, 10), Moscow.At(2, 11))));

        // Итог в виде «201: 1, 409 booking.time_conflict: 9» — по нему сразу видно, что пошло не так, если тест упадёт.
        var outcome = string.Join(", ", (await Task.WhenAll(responses.Select(async r =>
                $"{(int)r.StatusCode} {(r.IsSuccessStatusCode ? "" : await TestApi.ErrorCodeAsync(r))}".Trim())))
            .GroupBy(x => x)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}: {g.Count()}"));

        Assert.Equal("201: 1, 409 booking.time_conflict: 9", outcome);
    }

    [Fact]
    public async Task Cancellation_is_recorded_in_history_and_cannot_be_repeated()
    {
        var admin = await _api.LoginAdminAsync();
        var resource = await _api.CreateResourceAsync(admin);
        var user = await _api.RegisterAsync();
        var booking = await TestApi.BookOkAsync(user, resource.Id, Moscow.At(3, 10), Moscow.At(3, 11));

        var cancel = await user.Client.PostAsync($"/api/bookings/{booking.Id}/cancel", null);
        Assert.Equal(BookingStatus.Cancelled, (await TestApi.ReadAsync<BookingDto>(cancel)).Status);

        var history = await user.Client.GetFromJsonAsync<List<AuditLogDto>>($"/api/bookings/{booking.Id}/history", TestApi.Json);
        Assert.Equal([AuditAction.BookingCreated, AuditAction.BookingCancelled], history!.Select(h => h.Action));

        var again = await user.Client.PostAsync($"/api/bookings/{booking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("booking.invalid_status_transition", await TestApi.ErrorCodeAsync(again));
    }

    [Fact]
    public async Task Available_search_skips_busy_resources_and_respects_filters()
    {
        var admin = await _api.LoginAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var busy = await _api.CreateResourceAsync(admin, name: $"Поиск {marker} занятая");
        var free = await _api.CreateResourceAsync(admin, name: $"Поиск {marker} свободная");
        var user = await _api.RegisterAsync();

        await TestApi.BookOkAsync(user, busy.Id, Moscow.At(4, 12), Moscow.At(4, 13));

        var from = Uri.EscapeDataString(Moscow.At(4, 12, 30).ToString("O"));
        var to = Uri.EscapeDataString(Moscow.At(4, 13, 30).ToString("O"));
        var page = await _api.Anonymous().GetFromJsonAsync<PagedResult<ResourceDto>>(
            $"/api/resources/available?search={marker}&from={from}&to={to}", TestApi.Json);

        Assert.Equal([free.Id], page!.Items.Select(r => r.Id));

        // Вечером ресурс закрыт (работает до 21:00) — в поиске его тоже нет.
        var lateFrom = Uri.EscapeDataString(Moscow.At(4, 20, 30).ToString("O"));
        var lateTo = Uri.EscapeDataString(Moscow.At(4, 21, 30).ToString("O"));
        var late = await _api.Anonymous().GetFromJsonAsync<PagedResult<ResourceDto>>(
            $"/api/resources/available?search={marker}&from={lateFrom}&to={lateTo}", TestApi.Json);

        Assert.Empty(late!.Items);
    }
}
