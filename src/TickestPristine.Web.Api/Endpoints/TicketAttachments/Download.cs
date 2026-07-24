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
            IQueryHandler<DownloadTicketAttachmentQuery, TicketAttachmentDownloadResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new DownloadTicketAttachmentQuery(attachmentId);

            Result<TicketAttachmentDownloadResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(
                response => Results.File(response.Content, response.ContentType, response.FileName),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.TicketAttachments);
    }
}
