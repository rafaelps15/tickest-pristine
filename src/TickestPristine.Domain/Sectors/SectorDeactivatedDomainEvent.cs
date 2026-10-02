using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public sealed record SectorDeactivatedDomainEvent(Guid SectorId) : IDomainEvent;
