namespace TickestPristine.Domain.Users;

public sealed class UserPermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PermissionCode { get; set; }
}
