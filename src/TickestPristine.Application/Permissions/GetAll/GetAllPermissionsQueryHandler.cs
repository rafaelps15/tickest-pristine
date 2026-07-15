using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Permissions.GetAll;

internal sealed class GetAllPermissionsQueryHandler : IQueryHandler<GetAllPermissionsQuery, IReadOnlyList<string>>
{
    public Task<Result<IReadOnlyList<string>>> Handle(GetAllPermissionsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(PermissionCodes.All));
}
