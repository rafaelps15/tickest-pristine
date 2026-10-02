using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Permissions.GetAll;

public sealed record GetPermissionsQuery : IQuery<List<PermissionResponse>>;
