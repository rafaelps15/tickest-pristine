using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Permissions.GetAll;

internal sealed class GetPermissionsQueryHandler : IQueryHandler<GetPermissionsQuery, List<PermissionResponse>>
{
    public Task<Result<List<PermissionResponse>>> Handle(GetPermissionsQuery query, CancellationToken cancellationToken)
    {
        var permissions = PermissionCodes.Definitions
            .Select(d => new PermissionResponse
            {
                Code = d.Code,
                Name = d.Name,
                Description = d.Description,
                Group = d.Group,
                IsAdministrative = d.IsAdministrative
            })
            .ToList();

        return Task.FromResult<Result<List<PermissionResponse>>>(permissions);
    }
}
