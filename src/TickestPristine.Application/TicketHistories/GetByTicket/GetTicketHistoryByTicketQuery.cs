using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketHistories.GetByTicket;

public sealed record GetTicketHistoryByTicketQuery(Guid TicketId) : IQuery<List<TicketHistoryResponse>>;
