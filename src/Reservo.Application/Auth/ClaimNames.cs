namespace Reservo.Application.Auth;

/// <summary>
/// Имена claims внутри нашего JWT. Ими пользуются и код, который выдаёт токены (Infrastructure),
/// и код, который их читает (API), — поэтому они не могут разойтись.
/// </summary>
public static class ClaimNames
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
