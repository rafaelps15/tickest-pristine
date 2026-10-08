using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.GetOwnSummary;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetOwnSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/mine/summary", async (
            IQueryHandler<GetOwnTicketSummaryQuery, List<OwnTicketCountResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<List<OwnTicketCountResponse>> result = await handler.Handle(new GetOwnTicketSummaryQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.TicketsViewOwn)
        .WithTags(Tags.Tickets);
    }
}
