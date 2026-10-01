using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public sealed record SectorUpdatedDomainEvent(Guid SectorId) : IDomainEvent;
