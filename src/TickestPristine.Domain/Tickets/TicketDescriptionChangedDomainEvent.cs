using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketDescriptionChangedDomainEvent(Guid TicketId, string PreviousDescription) : IDomainEvent;
