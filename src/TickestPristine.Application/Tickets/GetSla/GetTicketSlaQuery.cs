using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetSla;

public sealed record GetTicketSlaQuery(Guid TicketId) : IQuery<TicketSlaResponse>;
