using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Roles.GetAll;

public sealed record GetRolesQuery : IQuery<List<RoleResponse>>;
