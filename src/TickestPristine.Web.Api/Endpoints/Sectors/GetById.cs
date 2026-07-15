using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Sectors.GetById;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Sectors;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("sectors/{sectorId}", async (
            Guid sectorId,
            IQueryHandler<GetSectorByIdQuery, SectorResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetSectorByIdQuery(sectorId);

            Result<SectorResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Sectors);
    }
}
