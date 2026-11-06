using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.TicketMessages;

public sealed record TicketMessagePostedDomainEvent(Guid MessageId, Guid TicketId, Guid AuthorUserId) : IDomainEvent;
