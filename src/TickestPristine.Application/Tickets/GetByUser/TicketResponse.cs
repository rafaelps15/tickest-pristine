using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetByUser;

public sealed class TicketResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }
}
