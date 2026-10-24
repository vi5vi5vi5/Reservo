namespace Reservo.Infrastructure.Persistence;

/// <summary>Настройки демо-данных из секции "Seed" конфигурации.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public AdminSeedOptions Admin { get; init; } = new();

    /// <summary>
    /// Пароль демо-пользователей (студенты, преподаватель). Если пусто, ни они, ни демо-брони не создаются.
    /// На сервере задаётся переменной Seed__DemoUserPassword.
    /// </summary>
    public string? DemoUserPassword { get; init; }
}

public sealed class AdminSeedOptions
{
    public string Email { get; init; } = "admin@reservo.example";

    public string FullName { get; init; } = "Администратор";

    /// <summary>Если пусто, администратор не создаётся. На сервере задаётся переменной Seed__Admin__Password.</summary>
    public string? Password { get; init; }
}
