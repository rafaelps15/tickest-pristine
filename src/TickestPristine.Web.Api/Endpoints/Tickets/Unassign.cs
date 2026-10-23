using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.Unassign;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class Unassign : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("tickets/{ticketId:guid}/assignee", async (
            Guid ticketId,
            ICommandHandler<UnassignTicketCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UnassignTicketCommand(ticketId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsManage);
    }
}
