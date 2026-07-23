using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Sectors.GetAll;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Sectors;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("sectors", async (
            IQueryHandler<GetSectorsQuery, List<SectorResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<List<SectorResponse>> result = await handler.Handle(new GetSectorsQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Sectors);
    }
}
