namespace TickestPristine.Application.Authorization;

public static class PermissionCodes
{
    public static class Tickets
    {
        public const string Create = "tickets:create";
        public const string ViewOwn = "tickets:view-own";
        public const string UpdateOwn = "tickets:update-own";
        public const string Manage = "tickets:manage";
        public const string Delete = "tickets:delete";
        public const string Reopen = "tickets:reopen";
    }

    public static class Users
    {
        public const string Access = "users:access";
        public const string Manage = "users:manage";
        public const string Delete = "users:delete";
        public const string ManageRoles = "users:manage-roles";
    }

    public static class Roles
    {
        public const string Manage = "roles:manage";
    }

    public static IReadOnlyList<string> All { get; } =
    [
        Tickets.Create,
        Tickets.ViewOwn,
        Tickets.UpdateOwn,
        Tickets.Manage,
        Tickets.Delete,
        Tickets.Reopen,
        Users.Access,
        Users.Manage,
        Users.Delete,
        Users.ManageRoles,
        Roles.Manage
    ];
}
