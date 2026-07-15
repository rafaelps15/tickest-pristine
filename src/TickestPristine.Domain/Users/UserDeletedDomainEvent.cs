using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserDeletedDomainEvent(Guid UserId) : IDomainEvent;
