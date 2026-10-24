using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketCreatedDomainEvent(Guid TicketId, Guid OpenedByUserId) : IDomainEvent;
