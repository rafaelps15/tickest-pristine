using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetByUser;

public sealed record GetTicketsByUserQuery(Guid UserId) : IQuery<List<TicketResponse>>;
