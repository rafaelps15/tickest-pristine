using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserSectorChangedDomainEvent(Guid UserId, Guid? OldSectorId, Guid? NewSectorId) : IDomainEvent;
