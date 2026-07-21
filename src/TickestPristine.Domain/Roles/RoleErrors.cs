using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public static class RoleErrors
{
    public static Error NotFound(Guid roleId) => Error.NotFound(
        "Roles.NotFound",
        $"The role with the Id = '{roleId}' was not found");

    public static readonly Error NameNotUnique = Error.Conflict(
        "Roles.NameNotUnique",
        "The provided role name is not unique");
}
