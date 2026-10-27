using Microsoft.AspNetCore.Mvc;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketAttachments.Upload;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketAttachments;

internal sealed class Upload : IEndpoint
{
    // Os 10 MB do anexo mais uma folga para o envelope do multipart: acima disso, o corpo é recusado (413) antes de ser
    // lido, em vez de chegar inteiro ao validator. O limite do Kestrel vale para a requisição toda; o do formulário é
    // conferido pelo próprio ASP.NET ao ler o multipart, inclusive no TestServer dos testes de integração. Os dois
    // chegam ao GlobalExceptionHandler, que responde 413 em Problem Details.
    private const long MaxRequestBodySizeBytes = 11 * 1024 * 1024;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("tickets/{ticketId:guid}/attachments", async (
            Guid ticketId,
            IFormFile file,
            ICommandHandler<UploadTicketAttachmentCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            await using Stream content = file.OpenReadStream();

            var command = new UploadTicketAttachmentCommand
            {
                TicketId = ticketId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                // Bytes efetivamente recebidos na parte do arquivo, e não um tamanho declarado pelo cliente.
                FileSizeBytes = file.Length,
                Content = content
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.TicketAttachments)
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingPolicies.AttachmentUpload)
        .DisableAntiforgery()
        .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodySizeBytes))
        .WithFormOptions(multipartBodyLengthLimit: MaxRequestBodySizeBytes);
    }
}
