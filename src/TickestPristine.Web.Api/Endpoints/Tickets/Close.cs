using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.Close;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class Close : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("tickets/{ticketId:guid}/close", async (
            Guid ticketId,
            ICommandHandler<CloseTicketCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CloseTicketCommand(ticketId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsManage);
    }
}
