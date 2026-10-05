using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Roles;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Permissions.GetAll;

internal sealed class GetPermissionsQueryHandler : IQueryHandler<GetPermissionsQuery, List<PermissionResponse>>
{
    public Task<Result<List<PermissionResponse>>> Handle(GetPermissionsQuery query, CancellationToken cancellationToken)
    {
        var permissions = Permission.All
            .Select(p => new PermissionResponse
            {
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Group = p.Group,
                IsAdministrative = p.IsAdministrative
            })
            .ToList();

        return Task.FromResult<Result<List<PermissionResponse>>>(permissions);
    }
}
