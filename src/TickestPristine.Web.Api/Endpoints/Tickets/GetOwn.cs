using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets.GetOwn;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetOwn : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/mine", async (
            OwnTicketStatusGroup? status,
            string? search,
            [AsParameters] PageRequest paging,
            IQueryHandler<GetOwnTicketsQuery, PagedResponse<OwnTicketResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOwnTicketsQuery(status, search, paging);

            Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsViewOwn);
    }
}
