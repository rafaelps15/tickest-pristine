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
            TicketSlaStatus? slaStatus,
            string? search,
            [AsParameters] PageRequest paging,
            IQueryHandler<GetTicketsQuery, PagedResponse<TicketSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketsQuery(
                statuses ?? [],
                priority,
                sectorId,
                slaStatus,
                search,
                paging);

            Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.TicketsManage)
        .WithTags(Tags.Tickets);
    }
}
