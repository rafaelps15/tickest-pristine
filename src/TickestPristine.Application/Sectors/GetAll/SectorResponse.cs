namespace TickestPristine.Application.Sectors.GetAll;

public sealed class SectorResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }
}
