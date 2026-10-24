using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetStatusCounts;

internal sealed class GetTicketStatusCountsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetTicketStatusCountsQuery, TicketStatusCountsResponse>
{
    public async Task<Result<TicketStatusCountsResponse>> Handle(
        GetTicketStatusCountsQuery query,
        CancellationToken cancellationToken)
    {
        TicketStatusCountsResponse? counts = await context.Tickets
            .GroupBy(t => 1)
            .Select(g => new TicketStatusCountsResponse
            {
                TotalCount = g.Count(),
                OpenCount = g.Count(t => t.Status == TicketStatus.Open),
                InProgressCount = g.Count(t => t.Status == TicketStatus.InProgress),
                ResolvedCount = g.Count(t => t.Status == TicketStatus.Resolved),
                ClosedCount = g.Count(t => t.Status == TicketStatus.Closed),
                CanceledCount = g.Count(t => t.Status == TicketStatus.Canceled)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return counts ?? new TicketStatusCountsResponse();
    }
}
