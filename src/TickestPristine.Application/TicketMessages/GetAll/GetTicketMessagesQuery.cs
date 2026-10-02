using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketMessages.GetAll;

public sealed record GetTicketMessagesQuery(Guid TicketId) : IQuery<List<TicketMessageResponse>>;
