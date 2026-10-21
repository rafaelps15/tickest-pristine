using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.AssignRoles;

public sealed record AssignUserRolesCommand(Guid UserId, List<string> RoleNames) : ICommand;
