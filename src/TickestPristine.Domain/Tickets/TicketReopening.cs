using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketReopening(TicketHistoryEntry History, TicketSlaTimer ResolutionCycle);
