using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.GetSlaSummary;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetSlaSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/sla-summary", async (
            Guid? sectorId,
            IQueryHandler<GetTicketSlaSummaryQuery, TicketSlaSummaryResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketSlaSummaryQuery(sectorId);

            Result<TicketSlaSummaryResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsManage);
    }
}
