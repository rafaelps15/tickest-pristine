using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketHistoryRecordedDomainEvent(Guid HistoryId, Guid TicketId) : IDomainEvent;
