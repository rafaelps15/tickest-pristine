using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.GetSla;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetSla : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/{ticketId:guid}/sla", async (
            Guid ticketId,
            IQueryHandler<GetTicketSlaQuery, TicketSlaResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketSlaQuery(ticketId);

            Result<TicketSlaResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .RequireAuthorization();
    }
}
