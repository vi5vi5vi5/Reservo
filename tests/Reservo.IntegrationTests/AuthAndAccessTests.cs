using System.Net;
using System.Net.Http.Json;
using Reservo.Application.Auth;
using Reservo.Application.Users;
using Reservo.IntegrationTests.Infrastructure;

namespace Reservo.IntegrationTests;

public class AuthAndAccessTests(ReservoApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Register_then_login_then_me()
    {
        var email = $"ivan-{Guid.NewGuid():N}@test.example";
        var anonymous = _api.Anonymous();

        var registered = await anonymous.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Secret123", "Иван Петров"), TestApi.Json);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email.ToUpperInvariant(), "Secret123"), TestApi.Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await TestApi.ReadAsync<AuthResponse>(login);

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        me.Headers.Authorization = new("Bearer", auth.AccessToken);
        var profile = await TestApi.ReadAsync<UserDto>(await anonymous.SendAsync(me));

        Assert.Equal(email, profile.Email);
        Assert.Equal("Иван Петров", profile.FullName);
    }

    [Fact]
    public async Task Second_registration_with_same_email_is_409()
    {
        var email = $"twin-{Guid.NewGuid():N}@test.example";
        var anonymous = _api.Anonymous();

        await anonymous.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Secret123", "Первый"), TestApi.Json);
        var second = await anonymous.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Secret123", "Второй"), TestApi.Json);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("auth.email_taken", await TestApi.ErrorCodeAsync(second));
    }

    [Fact]
    public async Task Refresh_token_rotates_and_reuse_closes_all_sessions()
    {
        var user = await _api.RegisterAsync();
        var anonymous = _api.Anonymous();

        var rotated = await anonymous.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(user.Auth.RefreshToken), TestApi.Json);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var newPair = await TestApi.ReadAsync<AuthResponse>(rotated);
        Assert.NotEqual(user.Auth.RefreshToken, newPair.RefreshToken);

        // Старый токен уже обменян: повторная попытка выглядит как кража и закрывает все сессии.
        var reused = await anonymous.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(user.Auth.RefreshToken), TestApi.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        var afterReuse = await anonymous.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(newPair.RefreshToken), TestApi.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Request_without_token_is_401()
    {
        var response = await _api.Anonymous().GetAsync("/api/bookings/my");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await TestApi.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Regular_user_on_admin_endpoint_is_403()
    {
        var user = await _api.RegisterAsync();

        var response = await user.Client.GetAsync("/api/admin/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Someone_elses_booking_is_404()
    {
        var admin = await _api.LoginAdminAsync();
        var resource = await _api.CreateResourceAsync(admin);
        var owner = await _api.RegisterAsync();
        var stranger = await _api.RegisterAsync();
        var booking = await TestApi.BookOkAsync(owner, resource.Id, Moscow.At(1, 10), Moscow.At(1, 11));

        var response = await stranger.Client.GetAsync($"/api/bookings/{booking.Id}");

        // Не 403: чужая бронь для пользователя как будто не существует.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
