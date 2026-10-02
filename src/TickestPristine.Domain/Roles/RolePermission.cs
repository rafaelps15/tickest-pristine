using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class RolePermission : Entity
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; }
}
