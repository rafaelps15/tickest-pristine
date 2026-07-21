namespace TickestPristine.Application.Roles.GetAll;

public sealed class RoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public List<string> PermissionCodes { get; set; } = [];
}
