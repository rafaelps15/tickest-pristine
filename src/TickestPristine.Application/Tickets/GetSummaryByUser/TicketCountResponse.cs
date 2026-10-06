using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetSummaryByUser;

public sealed class TicketCountResponse
{
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public int Count { get; set; }
}
