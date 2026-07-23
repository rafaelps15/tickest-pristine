using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Sectors.Delete;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Sectors;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("sectors/{sectorId:guid}", async (
            Guid sectorId,
            ICommandHandler<DeleteSectorCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteSectorCommand(sectorId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Sectors.Manage)
        .WithTags(Tags.Sectors);
    }
}
