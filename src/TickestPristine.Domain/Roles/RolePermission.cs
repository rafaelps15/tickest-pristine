using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class RolePermission : Entity, IAuditable
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; }
}
