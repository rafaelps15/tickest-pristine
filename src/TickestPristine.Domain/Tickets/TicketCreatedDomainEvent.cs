using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketCreatedDomainEvent(Guid TicketId) : IDomainEvent;
