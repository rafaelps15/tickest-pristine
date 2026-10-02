using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
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
                IsDefault = r.IsDefault,
                IsAdministrator = r.IsAdministrator,
                PermissionCodes = context.RolePermissions
                    .Where(p => p.RoleId == r.Id)
                    .Select(p => p.PermissionCode)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return roles;
    }
}
