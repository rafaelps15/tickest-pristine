using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.Create;

public sealed class CreateTicketCommand : ICommand<Guid>
{
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
    public Guid? RequesterId { get; set; }
    public Guid? ResponsibleId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid SectorId { get; set; }
}
