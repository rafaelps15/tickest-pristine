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
            HttpContext httpContext,
            IQueryHandler<GetTicketAttachmentContentQuery, TicketAttachmentResponse> handler,
            CancellationToken cancellationToken) =>
        {
            // O tipo do arquivo veio do cliente no upload: o navegador não deve tentar adivinhar outro pelo conteúdo.
            httpContext.Response.Headers.XContentTypeOptions = "nosniff";

            var query = new GetTicketAttachmentContentQuery(attachmentId);

            Result<TicketAttachmentResponse> result = await handler.Handle(query, cancellationToken);

            // Com o nome do arquivo, a resposta leva "Content-Disposition: attachment": o navegador baixa o arquivo em
            // vez de abri-lo na própria origem da API.
            return result.Match(
                attachment => Results.File(attachment.Content, attachment.ContentType, attachment.FileName),
                CustomResults.Problem);
        })
        .WithTags(Tags.TicketAttachments)
        .RequireAuthorization();
    }
}
