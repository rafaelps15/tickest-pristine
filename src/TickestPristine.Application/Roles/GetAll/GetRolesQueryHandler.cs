using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Roles.GetAll;

internal sealed class GetRolesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRolesQuery, List<RoleResponse>>
{
    public async Task<Result<List<RoleResponse>>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
    {
        List<RoleResponse> roles = await context.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                IsDefault = r.Id == RoleIds.Collaborator,
                IsAdministrator = r.Id == RoleIds.Administrator,
                IsSystem = RoleIds.System.Contains(r.Id),
                ActiveUserCount = context.UserRoles.Count(userRole =>
                    userRole.RoleId == r.Id && context.Users.Any(user => user.Id == userRole.UserId && user.IsActive)),
                UserCount = context.UserRoles.Count(userRole => userRole.RoleId == r.Id),
                PermissionCodes = context.RolePermissions
                    .Where(p => p.RoleId == r.Id)
                    .Select(p => p.PermissionCode)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return roles;
    }
}
