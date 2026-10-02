using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserActivatedDomainEvent(Guid UserId) : IDomainEvent;
