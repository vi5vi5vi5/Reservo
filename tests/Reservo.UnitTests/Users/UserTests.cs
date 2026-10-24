using Reservo.Domain.Users;

namespace Reservo.UnitTests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Email_is_trimmed_and_lowercased()
    {
        var user = new User("  Ivan.Petrov@Mail.RU ", "Иван", "hash", UserRole.User, Now);

        Assert.Equal("ivan.petrov@mail.ru", user.Email);
    }

    [Fact]
    public void Full_name_is_trimmed()
    {
        var user = new User("ivan@mail.ru", "  Иван Петров  ", "hash", UserRole.User, Now);

        Assert.Equal("Иван Петров", user.FullName);
    }

    [Fact]
    public void New_user_is_active()
    {
        var user = new User("ivan@mail.ru", "Иван", "hash", UserRole.User, Now);

        Assert.True(user.IsActive);
        Assert.Equal(Now, user.CreatedAt);
    }
}
