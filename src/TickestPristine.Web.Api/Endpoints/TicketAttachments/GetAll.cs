using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketAttachments.GetAll;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketAttachments;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/{ticketId:guid}/attachments", async (
            Guid ticketId,
            IQueryHandler<GetTicketAttachmentsQuery, List<TicketAttachmentResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketAttachmentsQuery(ticketId);

            Result<List<TicketAttachmentResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.TicketAttachments)
        .RequireAuthorization();
    }
}
