using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.AssignRoles;

public sealed class AssignUserRolesCommand : ICommand
{
    public Guid UserId { get; set; }
    public List<string> RoleNames { get; set; } = [];
}
