using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public sealed class Sector : Entity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public Guid DepartmentId { get; set; }
}
