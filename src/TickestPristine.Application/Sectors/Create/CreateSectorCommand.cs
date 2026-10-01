using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Sectors.Create;

public sealed class CreateSectorCommand : ICommand<Guid>
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }
}
