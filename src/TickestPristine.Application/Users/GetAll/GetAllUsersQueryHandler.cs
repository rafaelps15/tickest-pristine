using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetAll;

internal sealed class GetAllUsersQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllUsersQuery, List<UserSummaryResponse>>
{
    public async Task<Result<List<UserSummaryResponse>>> Handle(GetAllUsersQuery query, CancellationToken cancellationToken)
    {
        List<UserSummaryResponse> users = await context.Users
            .OrderBy(u => u.FirstName)
            .Select(u => new UserSummaryResponse
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName
            })
            .ToListAsync(cancellationToken);

        // No collection navigation exists from User/Role to UserRole (see UserRoleConfiguration),
        // so roles are fetched separately and grouped in memory instead of via .Include(...).
        var userRoles = await (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            select new { userRole.UserId, RoleId = role.Id, RoleName = role.Name })
            .ToListAsync(cancellationToken);

        ILookup<Guid, RoleSummaryResponse> rolesByUserId = userRoles.ToLookup(
            ur => ur.UserId,
            ur => new RoleSummaryResponse { Id = ur.RoleId, Name = ur.RoleName });

        foreach (UserSummaryResponse user in users)
        {
            user.Roles = rolesByUserId[user.Id].ToList();
        }

        return users;
    }
}
