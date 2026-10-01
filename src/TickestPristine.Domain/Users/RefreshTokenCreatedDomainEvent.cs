using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record RefreshTokenCreatedDomainEvent(Guid RefreshTokenId, Guid UserId) : IDomainEvent;
