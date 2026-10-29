using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Roles.GetById;

public sealed record GetRoleByIdQuery(Guid RoleId) : IQuery<RoleResponse>;
