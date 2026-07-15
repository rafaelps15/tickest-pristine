using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Tickets.Delete;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("tickets/{ticketId:guid}", async (
            Guid ticketId,
            ICommandHandler<DeleteTicketCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteTicketCommand(ticketId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Tickets.Delete)
        .WithTags(Tags.Tickets);
    }
}
