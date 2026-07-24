using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketMessageEditedDomainEvent(Guid MessageId, Guid TicketId) : IDomainEvent;
