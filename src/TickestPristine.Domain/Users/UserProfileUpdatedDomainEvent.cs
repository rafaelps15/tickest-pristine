using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserProfileUpdatedDomainEvent(Guid UserId, string FirstName, string LastName) : IDomainEvent;
