using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users.AssignRoles;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class AssignRoles : IEndpoint
{
    public sealed class Request
    {
        public List<string> RoleNames { get; set; } = [];
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/roles", async (
            Guid userId,
            Request request,
            ICommandHandler<AssignUserRolesCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignUserRolesCommand
            {
                UserId = userId,
                RoleNames = request.RoleNames
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersAssignRoles)
        .WithTags(Tags.Users);
    }
}
