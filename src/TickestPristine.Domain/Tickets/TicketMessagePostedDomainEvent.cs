using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketMessagePostedDomainEvent(Guid MessageId, Guid TicketId) : IDomainEvent;
