using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserRegisteredDomainEvent(Guid UserId) : IDomainEvent;
