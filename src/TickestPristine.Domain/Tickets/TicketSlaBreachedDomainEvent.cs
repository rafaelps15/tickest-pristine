using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketSlaBreachedDomainEvent(Guid TicketId, SlaMetric Metric, int Cycle, DateTime DueOnUtc) : IDomainEvent;
