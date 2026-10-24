namespace Reservo.Application.Auth;

/// <summary>
/// Claim names inside our JWT. Shared by the code that issues tokens (Infrastructure)
/// and the code that reads them (API), so the two can never drift apart.
/// </summary>
public static class ClaimNames
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
