using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public sealed record SectorCreatedDomainEvent(Guid SectorId) : IDomainEvent;
