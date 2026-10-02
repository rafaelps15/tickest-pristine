using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketMessages.GetAll;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketMessages;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/{ticketId:guid}/messages", async (
            Guid ticketId,
            IQueryHandler<GetTicketMessagesQuery, List<TicketMessageResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketMessagesQuery(ticketId);

            Result<List<TicketMessageResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.TicketMessages)
        .RequireAuthorization();
    }
}
