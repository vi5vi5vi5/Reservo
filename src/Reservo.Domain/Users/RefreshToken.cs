namespace Reservo.Domain.Users;

/// <summary>Почему refresh-токен перестал действовать.</summary>
public enum RefreshTokenRevocation
{
    /// <summary>Пользователь вышел.</summary>
    Logout,

    /// <summary>Токен обменяли на новый (ротация). Если такой токен придёт снова — его, скорее всего, украли.</summary>
    Rotated,

    /// <summary>Администратор заблокировал пользователя.</summary>
    UserBlocked,

    /// <summary>Закрыт после подозрения на кражу: кто-то повторно прислал уже обменянный токен этого пользователя.</summary>
    ReuseDetected,
}

/// <summary>
/// Refresh-токен: долгоживущий «пропуск» для получения нового access-токена без повторного ввода пароля.
/// В базе хранится только хеш токена — если базу украдут, токенами воспользоваться не получится.
/// </summary>
public class RefreshToken
{
    public const int TokenHashMaxLength = 128;

    // Нужен EF Core при загрузке из базы.
    private RefreshToken()
    {
    }

    public RefreshToken(int userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public int Id { get; private set; }

    public int UserId { get; private set; }

    public User User { get; private set; } = null!;

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Когда отозван.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Почему отозван.</summary>
    public RefreshTokenRevocation? RevokedReason { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>Отзывает токен. Повторный отзыв ничего не меняет: важны время и причина первого.</summary>
    public void Revoke(DateTimeOffset now, RefreshTokenRevocation reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedReason = reason;
    }
}
