using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketMessageDeletedDomainEvent(Guid MessageId, Guid TicketId) : IDomainEvent;
