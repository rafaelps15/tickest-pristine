using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed record RolePermissionsAssignedDomainEvent(
    Guid RoleId,
    IReadOnlyCollection<string> PreviousPermissionCodes,
    IReadOnlyCollection<string> PermissionCodes) : IDomainEvent;
