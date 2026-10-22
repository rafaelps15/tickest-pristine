using TickestPristine.Application.Abstractions.Messaging;

using TickestPristine.Application.Roles;

namespace TickestPristine.Application.Users.AssignRoles;

public sealed record AssignUserRolesCommand(Guid UserId, List<string> RoleNames) : ICommand;
