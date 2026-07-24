using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketMessages.Delete;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketMessages;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("ticket-messages/{messageId:guid}", async (
            Guid messageId,
            ICommandHandler<DeleteTicketMessageCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteTicketMessageCommand(messageId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.TicketMessages);
    }
}
