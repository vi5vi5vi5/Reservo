using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reservo.Application.Auth;
using Reservo.Application.Bookings;
using Reservo.Application.Catalog.Categories;
using Reservo.Application.Catalog.Locations;
using Reservo.Application.Catalog.Resources;

namespace Reservo.IntegrationTests.Infrastructure;

/// <summary>Вошедший пользователь: его HTTP-клиент уже отправляет токен в заголовке Authorization.</summary>
public sealed record Session(HttpClient Client, AuthResponse Auth)
{
    public int UserId => Auth.User.Id;
}

/// <summary>
/// Помощники для тестов. Каждый тест создаёт себе своих пользователей и свой ресурс,
/// поэтому тесты не мешают друг другу, хотя работают с одной базой и параллельно.
/// </summary>
public sealed class TestApi(ReservoApiFactory factory)
{
    /// <summary>Те же настройки JSON, что у API: camelCase и перечисления строками.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public HttpClient Anonymous() => factory.CreateClient();

    public async Task<Session> RegisterAsync(string fullName = "Тестовый пользователь")
    {
        var email = $"user-{Guid.NewGuid():N}@test.example";
        var response = await Anonymous().PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Secret123", fullName), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return SessionFor(await ReadAsync<AuthResponse>(response));
    }

    public async Task<Session> LoginAdminAsync()
    {
        var response = await Anonymous().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(ReservoApiFactory.AdminEmail, ReservoApiFactory.AdminPassword), Json);
        response.EnsureSuccessStatusCode();

        return SessionFor(await ReadAsync<AuthResponse>(response));
    }

    /// <summary>Отдельный ресурс для теста: переговорная на 10 человек, 08:00–21:00.</summary>
    public async Task<ResourceDto> CreateResourceAsync(Session admin, bool requiresApproval = false, string? name = null)
    {
        var category = (await admin.Client.GetFromJsonAsync<List<CategoryDto>>("/api/categories", Json))!.First();
        var location = (await admin.Client.GetFromJsonAsync<List<LocationDto>>("/api/locations", Json))!.First();

        var response = await admin.Client.PostAsJsonAsync("/api/resources", new ResourceRequest
        {
            CategoryId = category.Id,
            LocationId = location.Id,
            Name = name ?? $"Тестовая переговорная {Guid.NewGuid():N}",
            Capacity = 10,
            RequiresApproval = requiresApproval,
            OpenTime = new TimeOnly(8, 0),
            CloseTime = new TimeOnly(21, 0),
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAsync<ResourceDto>(response);
    }

    public static Task<HttpResponseMessage> BookAsync(Session user, int resourceId, DateTimeOffset start, DateTimeOffset end, string title = "Встреча") =>
        user.Client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            ResourceId = resourceId,
            StartAt = start,
            EndAt = end,
            Title = title,
            Attendees = 2,
        }, Json);

    public static async Task<BookingDto> BookOkAsync(Session user, int resourceId, DateTimeOffset start, DateTimeOffset end)
    {
        var response = await BookAsync(user, resourceId, start, end);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<BookingDto>(response);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Код ошибки из ProblemDetails, например <c>booking.time_conflict</c>.</summary>
    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private Session SessionFor(AuthResponse auth)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new Session(client, auth);
    }
}

/// <summary>Время по Москве: брони задаются в местном времени площадки, как это делает настоящий клиент.</summary>
public static class Moscow
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    /// <summary>Через <paramref name="days"/> дней от сегодняшнего (по Москве) в hh:mm.</summary>
    public static DateTimeOffset At(int days, int hour, int minute = 0)
    {
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).Date;
        return new DateTimeOffset(today.AddDays(days).AddHours(hour).AddMinutes(minute), TimeSpan.FromHours(3));
    }
}
