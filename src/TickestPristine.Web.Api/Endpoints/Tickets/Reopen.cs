using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.Reopen;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class Reopen : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("tickets/{ticketId:guid}/reopen", async (
            Guid ticketId,
            ICommandHandler<ReopenTicketCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ReopenTicketCommand(ticketId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Tickets);
    }
}
