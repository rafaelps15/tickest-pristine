using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Roles.AssignPermissions;

public sealed class AssignRolePermissionsCommand : ICommand
{
    public Guid RoleId { get; set; }
    public List<string> PermissionCodes { get; set; } = [];
}
