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
                Name = r.Name,
                IsDefault = r.Name == RoleNames.Collaborator,
                IsAdministrator = r.Name == RoleNames.Administrator,
                PermissionCodes = context.RolePermissions
                    .Where(p => p.RoleName == r.Name)
                    .Select(p => p.PermissionCode)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return roles;
    }
}
