using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserInvitationIssuedDomainEvent(Guid UserId) : IDomainEvent;
