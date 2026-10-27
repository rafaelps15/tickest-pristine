using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.TicketAttachments.GetPolicy;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.TicketAttachments;

internal sealed class GetPolicy : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("ticket-attachments/policy", async (
            IQueryHandler<GetTicketAttachmentPolicyQuery, TicketAttachmentPolicyResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketAttachmentPolicyQuery();

            Result<TicketAttachmentPolicyResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.TicketAttachments)
        .RequireAuthorization();
    }
}
