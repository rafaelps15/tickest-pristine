using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketUnassignedDomainEvent(
    Guid TicketId,
    Guid PreviousAssignedToUserId,
    Guid? ChangedByUserId) : IDomainEvent;
