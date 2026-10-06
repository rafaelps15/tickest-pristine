using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.GetById;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/{ticketId:guid}", async (
            Guid ticketId,
            IQueryHandler<GetTicketByIdQuery, TicketResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketByIdQuery(ticketId);

            Result<TicketResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .RequireAuthorization();
    }
}
