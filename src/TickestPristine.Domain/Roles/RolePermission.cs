using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class RolePermission : Entity, IAuditable
{
    private RolePermission() { }

    public Guid Id { get; private set; }
    public string RoleName { get; private set; }
    public string PermissionCode { get; private set; }

    /// <summary>Cria a permissão da função; chamado somente por <see cref="Role"/>.</summary>
    internal static RolePermission Create(string roleName, string permissionCode) => new()
    {
        Id = Guid.NewGuid(),
        RoleName = roleName,
        PermissionCode = permissionCode
    };
}
