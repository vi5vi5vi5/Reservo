using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Auth;
using Reservo.Domain.Bookings;
using Reservo.Domain.Users;

namespace Reservo.Infrastructure.Persistence.Seed;

/// <summary>
/// Демо-пользователи и брони на завтра и послезавтра (PROJECT.md, 11.4), чтобы расписание на защите не было пустым.
/// Брони добавляются один раз — в базу, где броней ещё нет. Правила бронирования здесь не проверяются:
/// данные заведомо корректны, а время считается от момента запуска.
/// </summary>
internal static class DemoSeeder
{
    private static readonly (string Email, string FullName)[] Users =
    [
        ("student@reservo.example", "Иван Петров"),
        ("teacher@reservo.example", "Мария Соколова"),
        ("student2@reservo.example", "Алексей Смирнов"),
    ];

    private sealed record BookingSeed(
        string Resource, string UserEmail, int DayOffset, (int H, int M) From, (int H, int M) To, string Title, int? Attendees);

    private static readonly BookingSeed[] Bookings =
    [
        // «Байкал» занят завтра 14:00–15:00: поиск «завтра 14:00–15:30, 8 человек, проектор» из демо найдёт «Онегу».
        new("Переговорная «Байкал»", "student@reservo.example", 1, (10, 0), (11, 30), "Подготовка к защите проекта", 5),
        new("Переговорная «Байкал»", "teacher@reservo.example", 1, (14, 0), (15, 0), "Консультация по дипломам", 4),
        new("Аудитория 305", "teacher@reservo.example", 1, (12, 0), (13, 30), "Семинар по базам данных", 25),
        new("Переговорная «Онега»", "student2@reservo.example", 2, (16, 0), (17, 0), "Встреча студенческого клуба", 10),
        // Ресурсы с согласованием: брони попадут в статус Pending — их можно одобрить или отклонить на демо.
        new("Лаборатория робототехники", "student@reservo.example", 2, (10, 0), (13, 0), "Сборка манипулятора", 3),
        new("3D-принтер Prusa MK4", "student2@reservo.example", 2, (9, 0), (15, 0), "Печать корпуса для робота", null),
    ];

    public static async Task SeedAsync(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        BookingPolicy policy,
        TimeProvider timeProvider,
        SeedOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.DemoUserPassword))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var users = await EnsureUsersAsync(db, passwordHasher, options.DemoUserPassword, now, logger, cancellationToken);

        if (await db.Bookings.AnyAsync(cancellationToken))
        {
            return;
        }

        var resourceNames = Bookings.Select(b => b.Resource).Distinct().ToArray();
        var resources = (await db.Resources.Where(r => resourceNames.Contains(r.Name)).ToListAsync(cancellationToken))
            .GroupBy(r => r.Name)
            .ToDictionary(g => g.Key, g => g.First());

        var today = policy.ToLocal(now).Date;

        // Местные дата и время площадки → момент времени в UTC.
        DateTimeOffset At(int dayOffset, (int H, int M) time)
        {
            var local = today.AddDays(dayOffset).AddHours(time.H).AddMinutes(time.M);
            return new DateTimeOffset(local, policy.TimeZone.GetUtcOffset(local)).ToUniversalTime();
        }

        var added = 0;
        foreach (var seed in Bookings)
        {
            if (!resources.TryGetValue(seed.Resource, out var resource) || !resource.IsActive)
            {
                continue;
            }

            db.Bookings.Add(new Booking(
                resource.Id,
                users[seed.UserEmail].Id,
                At(seed.DayOffset, seed.From),
                At(seed.DayOffset, seed.To),
                seed.Title,
                seed.Attendees,
                resource.RequiresApproval,
                now));
            added++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Добавлены демо-брони: {Count}", added);
    }

    private static async Task<Dictionary<string, User>> EnsureUsersAsync(
        AppDbContext db, IPasswordHasher passwordHasher, string password, DateTimeOffset now, ILogger logger, CancellationToken cancellationToken)
    {
        var emails = Users.Select(u => u.Email).ToArray();
        var existing = await db.Users.Where(u => emails.Contains(u.Email)).ToDictionaryAsync(u => u.Email, cancellationToken);

        var missing = Users.Where(u => !existing.ContainsKey(u.Email)).ToArray();
        if (missing.Length == 0)
        {
            return existing;
        }

        foreach (var (email, fullName) in missing)
        {
            var user = new User(email, fullName, passwordHasher.Hash(password), UserRole.User, now);
            db.Users.Add(user);
            existing[email] = user;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Добавлены демо-пользователи: {Emails}", string.Join(", ", missing.Select(u => u.Email)));

        return existing;
    }
}
