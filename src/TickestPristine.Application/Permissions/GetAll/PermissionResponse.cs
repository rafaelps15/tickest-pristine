namespace TickestPristine.Application.Permissions.GetAll;

public sealed class PermissionResponse
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Group { get; set; }
    public bool IsAdministrative { get; set; }
}
