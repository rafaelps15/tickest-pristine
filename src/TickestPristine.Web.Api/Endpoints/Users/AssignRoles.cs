using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Users.AssignRoles;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class AssignRoles : IEndpoint
{
    public sealed record Request(List<Guid> RoleIds);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/roles", async (
            Guid userId,
            Request request,
            ICommandHandler<AssignRolesCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignRolesCommand
            {
                UserId = userId,
                RoleIds = request.RoleIds
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Users.ManageRoles)
        .WithTags(Tags.Users);
    }
}
