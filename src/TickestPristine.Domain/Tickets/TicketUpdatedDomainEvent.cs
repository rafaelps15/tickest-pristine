using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketUpdatedDomainEvent(Guid TicketId) : IDomainEvent;
