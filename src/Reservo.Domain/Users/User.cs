namespace Reservo.Domain.Users;

public class User
{
    public const int EmailMaxLength = 256;
    public const int FullNameMaxLength = 200;

    // Нужен EF Core, когда он создаёт объекты пользователей, загруженных из базы.
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

    /// <summary>False — пользователь заблокирован и не может войти.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void ChangeRole(UserRole role) => Role = role;

    /// <summary>Заблокированный пользователь не может войти и обновить токен (BR-16).</summary>
    public void Block() => IsActive = false;

    public void Unblock() => IsActive = true;

    /// <summary>
    /// Email хранится и сравнивается в нижнем регистре, поэтому «Ivan@Mail.ru» и «ivan@mail.ru» — один и тот же аккаунт.
    /// </summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
