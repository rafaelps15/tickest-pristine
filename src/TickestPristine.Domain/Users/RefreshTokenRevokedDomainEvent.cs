using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record RefreshTokenRevokedDomainEvent(Guid RefreshTokenId, Guid UserId) : IDomainEvent;
