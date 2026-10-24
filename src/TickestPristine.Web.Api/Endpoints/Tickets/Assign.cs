using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.Assign;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class Assign : IEndpoint
{
    public sealed record Request(Guid AssignedToUserId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("tickets/{ticketId:guid}/assignee", async (
            Guid ticketId,
            Request request,
            ICommandHandler<AssignTicketCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignTicketCommand(ticketId, request.AssignedToUserId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsManage);
    }
}
