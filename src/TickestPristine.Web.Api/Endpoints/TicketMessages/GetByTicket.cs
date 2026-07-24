using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketMessages.GetByTicket;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketMessages;

internal sealed class GetByTicket : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/{ticketId:guid}/messages", async (
            Guid ticketId,
            IQueryHandler<GetTicketMessagesByTicketQuery, List<TicketMessageResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketMessagesByTicketQuery(ticketId);

            Result<List<TicketMessageResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.TicketMessages);
    }
}
