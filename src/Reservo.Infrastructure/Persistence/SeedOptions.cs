namespace Reservo.Infrastructure.Persistence;

/// <summary>Demo data settings from the "Seed" configuration section.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public AdminSeedOptions Admin { get; init; } = new();
}

public sealed class AdminSeedOptions
{
    public string Email { get; init; } = "admin@reservo.example";

    public string FullName { get; init; } = "Администратор";

    /// <summary>If empty, no administrator is created. On a server it comes from Seed__Admin__Password.</summary>
    public string? Password { get; init; }
}
