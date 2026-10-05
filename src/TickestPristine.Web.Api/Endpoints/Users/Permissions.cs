using TickestPristine.Domain.Roles;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal static class Permissions
{
    internal const string UsersRead = PermissionCodes.Users.Read;
    internal const string UsersDeactivate = PermissionCodes.Users.Deactivate;
    internal const string UsersAssignRoles = PermissionCodes.Users.AssignRoles;
}
