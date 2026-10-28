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
            [FromQuery(Name = "sectorId")] Guid[]? sectorIds,
            Guid? requesterId,
            Guid? assignedToUserId,
            bool? unassignedOnly,
            TicketSlaStatus? slaStatus,
            string? search,
            DateOnly? createdFrom,
            DateOnly? createdTo,
            DateOnly? resolvedFrom,
            DateOnly? resolvedTo,
            TicketSortOrder? sort,
            [AsParameters] PageRequest paging,
            IQueryHandler<GetTicketsQuery, PagedResponse<TicketSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketsQuery(
                statuses ?? [],
                priority,
                sectorIds ?? [],
                requesterId,
                assignedToUserId,
                unassignedOnly ?? false,
                slaStatus,
                search,
                createdFrom,
                createdTo,
                resolvedFrom,
                resolvedTo,
                sort ?? TicketSortOrder.Newest,
                paging);

            Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsManage);
    }
}
