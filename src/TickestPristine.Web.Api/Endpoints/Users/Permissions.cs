using TickestPristine.Application.Permissions;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal static class Permissions
{
    internal const string UsersRead = PermissionCodes.Users.Read;
    internal const string UsersCreate = PermissionCodes.Users.Create;
    internal const string UsersUpdate = PermissionCodes.Users.Update;
    internal const string UsersDeactivate = PermissionCodes.Users.Deactivate;
    internal const string UsersAssignRoles = PermissionCodes.Users.AssignRoles;
}
