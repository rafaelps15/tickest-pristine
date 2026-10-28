using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.Reopen;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class Reopen : IEndpoint
{
    /// <summary>
    /// Corpo opcional: sem ele, a reabertura é como antes; com o motivo, ele vira uma mensagem no chamado.
    /// </summary>
    public sealed class Request
    {
        public string? Reason { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("tickets/{ticketId:guid}/reopen", async (
            Guid ticketId,
            Request? request,
            ICommandHandler<ReopenTicketCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ReopenTicketCommand(ticketId, request?.Reason);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .RequireAuthorization();
    }
}
