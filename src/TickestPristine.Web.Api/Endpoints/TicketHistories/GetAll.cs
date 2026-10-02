using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketHistories.GetAll;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketHistories;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/{ticketId:guid}/history", async (
            Guid ticketId,
            IQueryHandler<GetTicketHistoryQuery, List<TicketHistoryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketHistoryQuery(ticketId);

            Result<List<TicketHistoryResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.TicketHistories)
        .RequireAuthorization();
    }
}
