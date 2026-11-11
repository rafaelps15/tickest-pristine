using TickestPristine.Application.Abstractions.Messaging;

using TickestPristine.Domain.Permissions;

namespace TickestPristine.Application.Roles.AssignPermissions;

public sealed class AssignRolePermissionsCommand : ICommand
{
    public string RoleName { get; set; }
    public List<string> PermissionCodes { get; set; } = [];
}

