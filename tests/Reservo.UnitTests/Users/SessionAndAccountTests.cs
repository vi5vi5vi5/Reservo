using FluentValidation.TestHelper;
using Reservo.Application.Users;
using Reservo.Domain.Users;

namespace Reservo.UnitTests.Users;

public class SessionAndAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static RefreshToken Token() => new(7, "hash", Now, Now.AddDays(14));

    [Fact]
    public void Fresh_refresh_token_is_active_until_it_expires()
    {
        var token = Token();

        Assert.True(token.IsActive(Now));
        Assert.True(token.IsActive(Now.AddDays(14).AddSeconds(-1)));
        Assert.False(token.IsActive(Now.AddDays(14)));
    }

    [Fact]
    public void Revoked_token_is_inactive_and_keeps_first_revocation()
    {
        var token = Token();

        token.Revoke(Now.AddHours(1), RefreshTokenRevocation.Rotated);
        token.Revoke(Now.AddHours(2), RefreshTokenRevocation.Logout);

        Assert.False(token.IsActive(Now.AddHours(1)));
        Assert.Equal(Now.AddHours(1), token.RevokedAt);
        Assert.Equal(RefreshTokenRevocation.Rotated, token.RevokedReason);
    }

    [Fact]
    public void Blocked_user_is_inactive_until_unblocked()
    {
        var user = new User("ivan@mail.ru", "Иван", "hash", UserRole.User, Now);

        user.Block();
        Assert.False(user.IsActive);

        user.Unblock();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Role_can_be_changed()
    {
        var user = new User("ivan@mail.ru", "Иван", "hash", UserRole.User, Now);

        user.ChangeRole(UserRole.Admin);

        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void Role_change_requires_role()
    {
        new ChangeRoleRequestValidator().TestValidate(new ChangeRoleRequest())
            .ShouldHaveValidationErrorFor(x => x.Role);
    }
}
