using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketMessages.GetByTicket;

public sealed record GetTicketMessagesByTicketQuery(Guid TicketId) : IQuery<List<TicketMessageResponse>>;
