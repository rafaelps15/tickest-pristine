using Microsoft.AspNetCore.Mvc;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets.GetAll;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets", async (
            [FromQuery(Name = "status")] TicketStatus[]? statuses,
            TicketPriority? priority,
            Guid? sectorId,
            Guid? requesterId,
            Guid? assignedToUserId,
            bool? unassignedOnly,
            TicketSlaStatus? slaStatus,
            string? search,
            TicketSortOrder? sort,
            [AsParameters] PageRequest paging,
            IQueryHandler<GetTicketsQuery, PagedResponse<TicketSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketsQuery(
                statuses ?? [],
                priority,
                sectorId,
                requesterId,
                assignedToUserId,
                unassignedOnly ?? false,
                slaStatus,
                search,
                sort ?? TicketSortOrder.Newest,
                paging);

            Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.TicketsManage)
        .WithTags(Tags.Tickets);
    }
}
