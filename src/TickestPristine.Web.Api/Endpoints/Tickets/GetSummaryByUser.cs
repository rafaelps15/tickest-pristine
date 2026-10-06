using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.GetSummaryByUser;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetSummaryByUser : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/users/{userId:guid}/summary", async (
            Guid userId,
            IQueryHandler<GetTicketSummaryByUserQuery, List<TicketCountResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketSummaryByUserQuery(userId);

            Result<List<TicketCountResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .RequireAuthorization();
    }
}
