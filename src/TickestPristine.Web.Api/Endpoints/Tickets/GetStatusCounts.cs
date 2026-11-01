using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.GetStatusCounts;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetStatusCounts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Só números agregados da empresa: atende qualquer usuário ativo (spec 031).
        app.MapGet("tickets/stats/status", async (
            IQueryHandler<GetTicketStatusCountsQuery, TicketStatusCountsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketStatusCountsQuery();

            Result<TicketStatusCountsResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .RequireAuthorization();
    }
}
