using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserRolesAssignedDomainEvent(Guid UserId) : IDomainEvent;
