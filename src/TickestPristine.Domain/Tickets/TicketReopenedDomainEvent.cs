using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketReopenedDomainEvent(Guid TicketId, Guid? ReopenedByUserId) : IDomainEvent;
