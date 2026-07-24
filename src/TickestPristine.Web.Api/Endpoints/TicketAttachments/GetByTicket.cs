using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketAttachments.GetByTicket;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketAttachments;

internal sealed class GetByTicket : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/{ticketId:guid}/attachments", async (
            Guid ticketId,
            IQueryHandler<GetTicketAttachmentsByTicketQuery, List<TicketAttachmentResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketAttachmentsByTicketQuery(ticketId);

            Result<List<TicketAttachmentResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.TicketAttachments);
    }
}
