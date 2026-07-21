namespace TickestPristine.Domain.Roles;

public sealed class RolePermission
{
    private RolePermission(Guid id, Guid roleId, string permissionCode)
    {
        Id = id;
        RoleId = roleId;
        PermissionCode = permissionCode;
    }

    public Guid Id { get; private set; }
    public Guid RoleId { get; private set; }
    public string PermissionCode { get; private set; }

    public static RolePermission Create(Guid roleId, string permissionCode) =>
        new(Guid.NewGuid(), roleId, permissionCode);
}
