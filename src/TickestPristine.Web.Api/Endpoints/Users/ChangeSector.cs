using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users.ChangeSector;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class ChangeSector : IEndpoint
{
    public sealed record Request(Guid? SectorId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/sector", async (
            Guid userId,
            Request request,
            ICommandHandler<ChangeUserSectorCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ChangeUserSectorCommand(userId, request.SectorId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersUpdate);
    }
}
