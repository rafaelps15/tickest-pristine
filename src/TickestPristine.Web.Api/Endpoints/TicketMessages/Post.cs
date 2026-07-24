using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketMessages.Post;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketMessages;

internal sealed class Post : IEndpoint
{
    public sealed class Request
    {
        public string Content { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("tickets/{ticketId:guid}/messages", async (
            Guid ticketId,
            Request request,
            ICommandHandler<PostTicketMessageCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new PostTicketMessageCommand
            {
                TicketId = ticketId,
                Content = request.Content
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.TicketMessages);
    }
}
