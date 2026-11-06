using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketMessagePosting(TicketMessage Message, TicketHistoryEntry History);
