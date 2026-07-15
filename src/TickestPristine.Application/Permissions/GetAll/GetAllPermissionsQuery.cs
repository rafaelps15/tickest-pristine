using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Permissions.GetAll;

public sealed record GetAllPermissionsQuery : IQuery<IReadOnlyList<string>>;
