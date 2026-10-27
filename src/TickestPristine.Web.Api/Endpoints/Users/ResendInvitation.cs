using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users.ResendInvitation;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class ResendInvitation : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/{userId:guid}/invitation", async (
            Guid userId,
            ICommandHandler<ResendUserInvitationCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ResendUserInvitationCommand(userId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersCreate);
    }
}
