using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed record RoleCreatedDomainEvent(Guid RoleId) : IDomainEvent;
