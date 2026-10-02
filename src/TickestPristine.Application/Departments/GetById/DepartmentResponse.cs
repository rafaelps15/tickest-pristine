namespace TickestPristine.Application.Departments.GetById;

public sealed class DepartmentResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public Guid? ResponsibleUserId { get; set; }
}
