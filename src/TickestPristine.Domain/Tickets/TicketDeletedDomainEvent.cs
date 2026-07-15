using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketDeletedDomainEvent(Guid TicketId) : IDomainEvent;
