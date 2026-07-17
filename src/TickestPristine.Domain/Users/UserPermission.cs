namespace TickestPristine.Domain.Users;

public sealed class UserPermission
{
    private UserPermission(Guid id, Guid userId, string permissionCode)
    {
        Id = id;
        UserId = userId;
        PermissionCode = permissionCode;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string PermissionCode { get; private set; }

    public static UserPermission Create(Guid userId, string permissionCode) =>
        new(Guid.NewGuid(), userId, permissionCode);
}
