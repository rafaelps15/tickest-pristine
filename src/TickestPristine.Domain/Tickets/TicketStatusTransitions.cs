namespace TickestPristine.Domain.Tickets;

public static class TicketStatusTransitions
{
    public static bool IsActive(TicketStatus status) => status is TicketStatus.Open or TicketStatus.InProgress;

    public static bool CanTransition(TicketStatus from, TicketStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return from switch
        {
            TicketStatus.Open => to is TicketStatus.InProgress or TicketStatus.Canceled,
            TicketStatus.InProgress => to is TicketStatus.Resolved or TicketStatus.Canceled,
            TicketStatus.Resolved => to is TicketStatus.Closed or TicketStatus.Open,
            TicketStatus.Closed => to is TicketStatus.Open,
            TicketStatus.Canceled => to is TicketStatus.Open,
            _ => false
        };
    }
}
