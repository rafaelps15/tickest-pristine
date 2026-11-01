namespace TickestPristine.Domain.Tickets;

public sealed record TicketReopening(TicketHistory History, TicketSlaTimer ResolutionCycle);
