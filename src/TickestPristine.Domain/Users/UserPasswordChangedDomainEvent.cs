using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserPasswordChangedDomainEvent(Guid UserId) : IDomainEvent;
