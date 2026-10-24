namespace Reservo.Domain.Users;

public class User
{
    public const int EmailMaxLength = 256;
    public const int FullNameMaxLength = 200;

    // Used by EF Core when it materializes users loaded from the database.
    private User()
    {
    }

    public User(string email, string fullName, string passwordHash, UserRole role, DateTimeOffset createdAt)
    {
        Email = NormalizeEmail(email);
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public string Email { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    /// <summary>False means the user is blocked and cannot sign in.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Emails are stored and compared in lower case, so "Ivan@Mail.ru" and "ivan@mail.ru" are the same account.
    /// </summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
