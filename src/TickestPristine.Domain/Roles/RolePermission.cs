using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class RolePermission : Entity, IAuditable
{
    public string RoleName { get; set; }
    public string PermissionCode { get; set; }
}
