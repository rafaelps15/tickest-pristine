using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketMessages.Edit;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketMessages;

internal sealed class Edit : IEndpoint
{
    public sealed class Request
    {
        public string Content { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("ticket-messages/{messageId:guid}", async (
            Guid messageId,
            Request request,
            ICommandHandler<EditTicketMessageCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new EditTicketMessageCommand
            {
                MessageId = messageId,
                Content = request.Content
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.TicketMessages);
    }
}
