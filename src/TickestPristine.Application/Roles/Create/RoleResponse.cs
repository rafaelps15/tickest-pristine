namespace TickestPristine.Application.Roles.Create;

/// <summary>
/// A role criada, no mesmo formato de um item de <c>GET roles</c> (spec 036, AC-1).
/// </summary>
public sealed class RoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
    public bool IsAdministrator { get; set; }
    public bool IsSystem { get; set; }
    public int ActiveUserCount { get; set; }
    public int UserCount { get; set; }
    public List<string> PermissionCodes { get; set; } = [];
}
