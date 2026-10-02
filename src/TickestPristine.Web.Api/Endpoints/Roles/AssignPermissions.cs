using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Roles.AssignPermissions;
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
        app.MapPut("roles/{roleId:guid}/permissions", async (
            Guid roleId,
            Request request,
            ICommandHandler<AssignRolePermissionsCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignRolePermissionsCommand
            {
                RoleId = roleId,
                PermissionCodes = request.PermissionCodes
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Roles)
        .HasPermission(PermissionCodes.Roles.Manage);
    }
}
