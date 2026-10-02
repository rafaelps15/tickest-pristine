using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public static class RoleErrors
{
    public static Error NotFound(Guid roleId) => Error.NotFound(
        "Roles.NotFound",
        $"A função com o Id = '{roleId}' não foi encontrada");

    public static Error PrivilegeEscalation(IEnumerable<string> permissionNames) => Error.Forbidden(
        "Roles.PrivilegeEscalation",
        $"Você não pode conceder nem remover permissões administrativas que não possui: {string.Join(", ", permissionNames)}");

    public static readonly Error NameNotUnique = Error.Conflict(
        "Roles.NameNotUnique",
        "O nome de função informado já está em uso");

    public static readonly Error AdministratorRoleIsManaged = Error.Conflict(
        "Roles.AdministratorRoleIsManaged",
        "As permissões da função de administrador são gerenciadas pelo sistema e não podem ser alteradas");

    public static readonly Error LastAdministrator = Error.Conflict(
        "Roles.LastAdministrator",
        "O sistema precisa manter pelo menos um administrador ativo");

    public static readonly Error DefaultRoleNotConfigured = Error.Failure(
        "Roles.DefaultRoleNotConfigured",
        "Nenhuma função padrão para novos usuários está configurada no sistema");
}
