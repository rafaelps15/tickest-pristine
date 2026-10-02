using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketAttachments.Download;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketAttachments;

internal sealed class Download : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("ticket-attachments/{attachmentId:guid}/download", async (
            Guid attachmentId,
            IQueryHandler<GetTicketAttachmentContentQuery, TicketAttachmentResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketAttachmentContentQuery(attachmentId);

            Result<TicketAttachmentResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(
                attachment => Results.File(attachment.Content, attachment.ContentType, attachment.FileName),
                CustomResults.Problem);
        })
        .WithTags(Tags.TicketAttachments)
        .RequireAuthorization();
    }
}
