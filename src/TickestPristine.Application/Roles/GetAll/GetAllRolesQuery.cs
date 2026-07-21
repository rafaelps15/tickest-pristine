using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Roles.GetAll;

public sealed record GetAllRolesQuery : IQuery<List<RoleResponse>>;
