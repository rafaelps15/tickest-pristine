using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketStatusChangedDomainEvent(
    Guid TicketId,
    TicketStatus OldStatus,
    TicketStatus NewStatus,
    Guid? ChangedByUserId) : IDomainEvent;
