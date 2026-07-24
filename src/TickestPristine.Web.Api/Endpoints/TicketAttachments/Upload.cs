using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketAttachments.Upload;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketAttachments;

internal sealed class Upload : IEndpoint
{
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
                FileSizeBytes = file.Length,
                Content = content
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithTags(Tags.TicketAttachments);
    }
}
