using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetOwnSummary;

public sealed class OwnTicketCountResponse
{
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public int Count { get; set; }
}
