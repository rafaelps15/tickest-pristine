using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Departments.Create;

public sealed class CreateDepartmentCommand : ICommand<Guid>
{
    public string Name { get; set; }
    public string Description { get; set; }
    public Guid? ResponsibleUserId { get; set; }
}
