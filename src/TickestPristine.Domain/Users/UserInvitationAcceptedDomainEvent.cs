using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed record UserInvitationAcceptedDomainEvent(Guid UserId) : IDomainEvent;
