using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketAttachments.Delete;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketAttachments;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("ticket-attachments/{attachmentId:guid}", async (
            Guid attachmentId,
            ICommandHandler<DeleteTicketAttachmentCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteTicketAttachmentCommand(attachmentId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.TicketAttachments);
    }
}
