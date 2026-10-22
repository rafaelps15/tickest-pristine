using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Roles.AssignPermissions;
using TickestPristine.Application.Permissions;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Roles;

internal sealed class AssignPermissions : IEndpoint
{
    public sealed class Request
    {
        public List<string> PermissionCodes { get; set; } = [];
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("roles/{roleName}/permissions", async (
            string roleName,
            Request request,
            ICommandHandler<AssignRolePermissionsCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignRolePermissionsCommand
            {
                RoleName = roleName,
                PermissionCodes = request.PermissionCodes
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Roles)
        .HasPermission(Permissions.RolesManage);
    }
}
