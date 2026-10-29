using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Roles.GetById;

internal sealed class GetRoleByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRoleByIdQuery, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(GetRoleByIdQuery query, CancellationToken cancellationToken)
    {
        RoleResponse? role = await context.Roles
            .Where(r => r.Id == query.RoleId)
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
            .SingleOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            return Result.Failure<RoleResponse>(RoleErrors.NotFound(query.RoleId));
        }

        return role;
    }
}
