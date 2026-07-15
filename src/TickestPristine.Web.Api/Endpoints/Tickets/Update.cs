using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.Update;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class Update : IEndpoint
{
    public sealed class Request
    {
        public string Description { get; set; }
        public TicketStatus Status { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("tickets/{ticketId:guid}", async (
            Guid ticketId,
            Request request,
            ICommandHandler<UpdateTicketCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateTicketCommand
            {
                TicketId = ticketId,
                Description = request.Description,
                Status = request.Status
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Tickets);
    }
}
