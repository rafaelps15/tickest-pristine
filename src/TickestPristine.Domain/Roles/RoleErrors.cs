using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public static class RoleErrors
{
    public static Error NotFound(Guid roleId) => Error.NotFound(
        "Roles.NotFound",
        $"A role com o Id = '{roleId}' não foi encontrada");

    public static readonly Error NameNotUnique = Error.Conflict(
        "Roles.NameNotUnique",
        "O nome de role informado já está em uso");

    public static readonly Error RequesterRoleNotConfigured = Error.Failure(
        "Roles.RequesterRoleNotConfigured",
        "A role padrão de solicitante não está configurada no sistema");
}
