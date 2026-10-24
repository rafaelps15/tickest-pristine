using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketReassignedDomainEvent(
    Guid TicketId,
    Guid? PreviousAssignedToUserId,
    Guid AssignedToUserId,
    Guid? ChangedByUserId) : IDomainEvent;
