using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketAssignedDomainEvent(
    Guid TicketId,
    Guid? PreviousAssignedToUserId,
    Guid AssignedToUserId,
    Guid? ChangedByUserId) : IDomainEvent;
