using Microsoft.EntityFrameworkCore;
using Reservo.Application.Auth;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Application.Common.Paging;

namespace Reservo.Application.Users;

public interface IUserService
{
    Task<UserDto> GetCurrentAsync(CancellationToken cancellationToken);

    Task<PagedResult<UserDto>> GetListAsync(UserListQuery query, CancellationToken cancellationToken);
}

public sealed class UserService(IAppDbContext db, ICurrentUser currentUser) : IUserService
{
    public async Task<UserDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        return user?.ToDto()
            ?? throw new AppException(ErrorKind.NotFound, "not_found", "Пользователь не найден.");
    }

    public async Task<PagedResult<UserDto>> GetListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            users = users.Where(u => u.Email.Contains(term) || u.FullName.ToLower().Contains(term));
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        // Projecting straight to the DTO means the SQL selects only these columns — never password_hash.
        return await users
            .OrderBy(u => u.Id)
            .Select(u => new UserDto(u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }
}
