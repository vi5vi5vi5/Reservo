using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Reservo.Application.Bookings;
using Reservo.Application.Common.Paging;
using Reservo.Application.Notifications;
using Reservo.Domain.Bookings;
using Reservo.Domain.Notifications;
using Reservo.IntegrationTests.Infrastructure;

namespace Reservo.IntegrationTests;

public class AdminFlowTests(ReservoApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Approval_confirms_booking_and_owner_gets_notification()
    {
        var admin = await _api.LoginAdminAsync();
        var lab = await _api.CreateResourceAsync(admin, requiresApproval: true);
        var student = await _api.RegisterAsync();

        var booking = await TestApi.BookOkAsync(student, lab.Id, Moscow.At(5, 10), Moscow.At(5, 12));
        Assert.Equal(BookingStatus.Pending, booking.Status);

        var approved = await admin.Client.PostAsync($"/api/admin/bookings/{booking.Id}/approve", null);
        Assert.Equal(BookingStatus.Confirmed, (await TestApi.ReadAsync<BookingDto>(approved)).Status);

        var notifications = await student.Client.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/notifications", TestApi.Json);
        Assert.Equal(
            [NotificationType.BookingApproved, NotificationType.BookingPendingApproval],
            notifications!.Items.Where(n => n.BookingId == booking.Id).Select(n => n.Type));
    }

    [Fact]
    public async Task Deactivation_cancels_future_bookings_and_notifies_owners()
    {
        var admin = await _api.LoginAdminAsync();
        var resource = await _api.CreateResourceAsync(admin);
        var owner = await _api.RegisterAsync();
        var booking = await TestApi.BookOkAsync(owner, resource.Id, Moscow.At(6, 10), Moscow.At(6, 11));

        var deactivate = await admin.Client.PostAsJsonAsync($"/api/resources/{resource.Id}/deactivate", new { reason = "Ремонт" }, TestApi.Json);
        deactivate.EnsureSuccessStatusCode();

        var cancelled = await owner.Client.GetFromJsonAsync<BookingDto>($"/api/bookings/{booking.Id}", TestApi.Json);
        Assert.Equal(BookingStatus.Cancelled, cancelled!.Status);
        Assert.Equal("Ресурс выведен из эксплуатации: Ремонт", cancelled.StatusReason);

        var notifications = await owner.Client.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/notifications", TestApi.Json);
        Assert.Contains(notifications!.Items, n => n.Type == NotificationType.BookingCancelled && n.BookingId == booking.Id);
    }

    [Fact]
    public async Task Owner_gets_live_notification_over_SignalR_when_admin_approves()
    {
        var admin = await _api.LoginAdminAsync();
        var lab = await _api.CreateResourceAsync(admin, requiresApproval: true);
        var student = await _api.RegisterAsync();

        var received = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduleChanged = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Настоящий клиент SignalR, подключённый к хабу тестового сервера.
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/live"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(student.Auth.AccessToken);
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
            .Build();

        connection.On<NotificationDto>("notificationReceived", n =>
        {
            if (n.Type == NotificationType.BookingApproved)
            {
                received.TrySetResult(n);
            }
        });
        connection.On<ScheduleChangedEvent>("scheduleChanged", e => scheduleChanged.TrySetResult(e.ResourceId));

        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeToResource", lab.Id);

        var booking = await TestApi.BookOkAsync(student, lab.Id, Moscow.At(7, 10), Moscow.At(7, 12));
        Assert.Equal(lab.Id, await scheduleChanged.Task.WaitAsync(TimeSpan.FromSeconds(10)));

        await admin.Client.PostAsync($"/api/admin/bookings/{booking.Id}/approve", null);

        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(booking.Id, notification.BookingId);
        Assert.Equal("Бронь одобрена", notification.Title);
    }

    private sealed record ScheduleChangedEvent(int ResourceId);
}
