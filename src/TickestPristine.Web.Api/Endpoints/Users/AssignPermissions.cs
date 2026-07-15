using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Users.AssignPermissions;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class AssignPermissions : IEndpoint
{
    public sealed record Request(List<string> PermissionCodes);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId}/permissions", async (
            Guid userId,
            Request request,
            ICommandHandler<AssignPermissionsCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignPermissionsCommand
            {
                UserId = userId,
                PermissionCodes = request.PermissionCodes
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Users.ManagePermissions)
        .WithTags(Tags.Users);
    }
}
