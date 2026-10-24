using Reservo.Domain.Users;

namespace Reservo.Application.Users;

/// <summary>Профиль пользователя. Хеш пароля наружу не отдаётся никогда.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Email">Email (в нижнем регистре).</param>
/// <param name="FullName">Имя и фамилия.</param>
/// <param name="Role">Роль: <c>User</c> или <c>Admin</c>.</param>
/// <param name="IsActive"><c>false</c> — пользователь заблокирован.</param>
/// <param name="CreatedAt">Дата регистрации (UTC).</param>
public sealed record UserDto(int Id, string Email, string FullName, UserRole Role, bool IsActive, DateTimeOffset CreatedAt);

public static class UserMappings
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.IsActive, user.CreatedAt);
}
