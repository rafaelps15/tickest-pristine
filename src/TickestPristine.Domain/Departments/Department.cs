using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Departments;

public sealed class Department : Entity, IAuditable
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public Guid? ResponsibleUserId { get; set; }
}
