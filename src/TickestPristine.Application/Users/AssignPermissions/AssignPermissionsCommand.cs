using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.AssignPermissions;

public sealed class AssignPermissionsCommand : ICommand
{
    public Guid UserId { get; set; }
    public List<string> PermissionCodes { get; set; } = [];
}
