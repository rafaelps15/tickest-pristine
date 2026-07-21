using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.AssignRoles;

public sealed class AssignRolesCommand : ICommand
{
    public Guid UserId { get; set; }
    public List<Guid> RoleIds { get; set; } = [];
}
