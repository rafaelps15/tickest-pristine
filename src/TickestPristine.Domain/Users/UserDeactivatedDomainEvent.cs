using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserDeactivatedDomainEvent(Guid UserId) : IDomainEvent;
