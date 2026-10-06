using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetSlaSummary;

internal sealed class GetTicketSlaSummaryQueryHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketSlaSummaryQuery, TicketSlaSummaryResponse>
{
    public async Task<Result<TicketSlaSummaryResponse>> Handle(GetTicketSlaSummaryQuery query, CancellationToken cancellationToken)
    {
        DateTime nowUtc = dateTimeProvider.UtcNow;

        IQueryable<Ticket> activeTickets = context.Tickets
            .WhereActive()
            .Where(t => query.SectorId == null || t.SectorId == query.SectorId);

        return new TicketSlaSummaryResponse
        {
            ActiveCount = await activeTickets.CountAsync(cancellationToken),
            OnTimeCount = await activeTickets.WhereSlaStatus(TicketSlaStatus.OnTime, nowUtc).CountAsync(cancellationToken),
            AtRiskCount = await activeTickets.WhereSlaStatus(TicketSlaStatus.AtRisk, nowUtc).CountAsync(cancellationToken),
            BreachedCount = await activeTickets.WhereSlaStatus(TicketSlaStatus.Breached, nowUtc).CountAsync(cancellationToken),
            ServerNowUtc = nowUtc
        };
    }
}
