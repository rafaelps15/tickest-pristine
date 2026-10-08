using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetOwnSummary;

internal sealed class GetOwnTicketSummaryQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetOwnTicketSummaryQuery, List<OwnTicketCountResponse>>
{
    public async Task<Result<List<OwnTicketCountResponse>>> Handle(
        GetOwnTicketSummaryQuery query,
        CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        List<OwnTicketCountResponse> counts = await context.Tickets
            .Where(t => t.CreatedByUserId == userId)
            .GroupBy(t => new { t.Priority, t.Status })
            .Select(g => new OwnTicketCountResponse
            {
                Priority = g.Key.Priority,
                Status = g.Key.Status,
                Count = g.Count()
            })
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Status)
            .ToListAsync(cancellationToken);

        return counts;
    }
}
