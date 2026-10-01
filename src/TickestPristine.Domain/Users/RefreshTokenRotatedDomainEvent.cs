using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record RefreshTokenRotatedDomainEvent(Guid RefreshTokenId, Guid UserId) : IDomainEvent;
