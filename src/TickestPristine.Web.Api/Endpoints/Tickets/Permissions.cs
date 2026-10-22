using TickestPristine.Application.Permissions;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal static class Permissions
{
    internal const string TicketsCreate = PermissionCodes.Tickets.Create;
    internal const string TicketsManage = PermissionCodes.Tickets.Manage;
    internal const string TicketsViewOwn = PermissionCodes.Tickets.ViewOwn;
}
