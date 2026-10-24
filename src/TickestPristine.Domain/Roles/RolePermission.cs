using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class RolePermission : Entity, IAuditable
{
    public Guid Id { get; set; }
    public string RoleName { get; set; }
    public string PermissionCode { get; set; }
}
