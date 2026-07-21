using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Roles.GetAll;

internal sealed class GetAllRolesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllRolesQuery, List<RoleResponse>>
{
    public async Task<Result<List<RoleResponse>>> Handle(GetAllRolesQuery query, CancellationToken cancellationToken)
    {
        List<RoleResponse> roles = await context.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                PermissionCodes = context.RolePermissions
                    .Where(p => p.RoleId == r.Id)
                    .Select(p => p.PermissionCode)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return roles;
    }
}
