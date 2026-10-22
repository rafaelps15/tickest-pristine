using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed record RolePermissionsAssignedDomainEvent(Guid RoleId) : IDomainEvent;
