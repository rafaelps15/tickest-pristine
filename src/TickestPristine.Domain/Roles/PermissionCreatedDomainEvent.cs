using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed record PermissionCreatedDomainEvent(Guid PermissionId) : IDomainEvent;
