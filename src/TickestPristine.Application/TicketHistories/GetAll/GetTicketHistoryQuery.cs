using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketHistories.GetAll;

public sealed record GetTicketHistoryQuery(Guid TicketId) : IQuery<List<TicketHistoryResponse>>;
