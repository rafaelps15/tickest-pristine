using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.GetVolume;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetVolume : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/stats/volume", async (
            TicketVolumeGrouping? groupBy,
            IQueryHandler<GetTicketVolumeQuery, List<TicketVolumeResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketVolumeQuery(groupBy ?? TicketVolumeGrouping.Day);

            Result<List<TicketVolumeResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsManage);
    }
}
