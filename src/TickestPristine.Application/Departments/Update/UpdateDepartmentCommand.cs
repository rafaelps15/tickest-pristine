using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Departments.Update;

public sealed class UpdateDepartmentCommand : ICommand
{
    public Guid DepartmentId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Guid? ResponsibleUserId { get; set; }
}
