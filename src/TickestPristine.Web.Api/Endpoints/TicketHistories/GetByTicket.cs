using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketHistories.GetByTicket;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketHistories;

internal sealed class GetByTicket : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/{ticketId:guid}/history", async (
            Guid ticketId,
            IQueryHandler<GetTicketHistoryByTicketQuery, List<TicketHistoryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketHistoryByTicketQuery(ticketId);

            Result<List<TicketHistoryResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.TicketHistories);
    }
}
