namespace TickestPristine.Domain.Sectors;

public sealed class Sector
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public Guid DepartmentId { get; set; }
}
