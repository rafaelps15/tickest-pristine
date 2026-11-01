using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets.GetAssignees;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetAssignees : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/assignees", async (
            string? search,
            Guid? sectorId,
            [AsParameters] PageRequest paging,
            IQueryHandler<GetTicketAssigneesQuery, PagedResponse<TicketAssigneeResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketAssigneesQuery(search, sectorId, paging);

            Result<PagedResponse<TicketAssigneeResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsManage);
    }
}
