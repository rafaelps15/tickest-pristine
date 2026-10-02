using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Sectors.Deactivate;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Sectors;

internal sealed class Deactivate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("sectors/{sectorId:guid}", async (
            Guid sectorId,
            ICommandHandler<DeactivateSectorCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeactivateSectorCommand(sectorId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Sectors)
        .HasPermission(PermissionCodes.Sectors.Manage);
    }
}
