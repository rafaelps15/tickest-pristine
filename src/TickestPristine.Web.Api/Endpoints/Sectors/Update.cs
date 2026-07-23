using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Sectors.Update;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Sectors;

internal sealed class Update : IEndpoint
{
    public sealed class Request
    {
        public string Name { get; set; }
        public string? Description { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("sectors/{sectorId:guid}", async (
            Guid sectorId,
            Request request,
            ICommandHandler<UpdateSectorCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateSectorCommand
            {
                SectorId = sectorId,
                Name = request.Name,
                Description = request.Description
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Sectors.Manage)
        .WithTags(Tags.Sectors);
    }
}
