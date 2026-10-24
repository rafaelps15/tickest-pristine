using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetById;

public sealed record GetTicketByIdQuery(Guid TicketId) : IQuery<TicketResponse>;
