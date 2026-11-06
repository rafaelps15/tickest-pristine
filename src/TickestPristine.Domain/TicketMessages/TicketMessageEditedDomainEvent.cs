using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.TicketMessages;

public sealed record TicketMessageEditedDomainEvent(Guid MessageId, Guid TicketId, Guid AuthorUserId, string PreviousContent)
    : IDomainEvent;
