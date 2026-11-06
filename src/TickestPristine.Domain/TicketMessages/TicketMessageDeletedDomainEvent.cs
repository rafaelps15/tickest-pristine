using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.TicketMessages;

public sealed record TicketMessageDeletedDomainEvent(Guid MessageId, Guid TicketId) : IDomainEvent;
