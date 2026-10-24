namespace TickestPristine.Application.Permissions;

/// <summary>
/// Códigos das permissões, como constantes para uso nos endpoints, nos handlers e nos testes. Cada código tem a sua
/// entrada no <see cref="PermissionCatalog"/>, com nome, descrição e grupo.
/// </summary>
public static class PermissionCodes
{
    public static class Tickets
    {
        public const string Create = "tickets:create";
        public const string ViewOwn = "tickets:view-own";
        public const string UpdateOwn = "tickets:update-own";
        public const string Manage = "tickets:manage";
        public const string DeleteOwn = "tickets:delete-own";
        public const string ReopenOwn = "tickets:reopen-own";
    }

    public static class Users
    {
        public const string Read = "users:read";
        public const string Create = "users:create";
        public const string Update = "users:update";
        public const string Deactivate = "users:deactivate";
        public const string AssignRoles = "users:assign-roles";
    }

    public static class Roles
    {
        public const string Read = "roles:read";
        public const string Manage = "roles:manage";
    }

    public static class Departments
    {
        public const string Manage = "departments:manage";
    }

    public static class Sectors
    {
        public const string Manage = "sectors:manage";
    }

    public static class Audit
    {
        public const string Read = "audit:read";
    }
}
