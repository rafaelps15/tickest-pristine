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

        IQueryable<Ticket> onTimeTickets = activeTickets.WhereSlaStatus(TicketSlaStatus.OnTime, nowUtc);
        IQueryable<Ticket> atRiskTickets = activeTickets.WhereSlaStatus(TicketSlaStatus.AtRisk, nowUtc);
        IQueryable<Ticket> breachedTickets = activeTickets.WhereSlaStatus(TicketSlaStatus.Breached, nowUtc);

        // Uma consulta só: as quatro contagens saem do mesmo instante do banco, sem um chamado mudar entre uma e outra.
        // Sem nenhum chamado ativo não há grupo, e o resumo sai zerado.
        TicketSlaSummaryResponse? summary = await activeTickets
            .GroupBy(t => 1)
            .Select(g => new TicketSlaSummaryResponse
            {
                ActiveCount = g.Count(),
                OnTimeCount = onTimeTickets.Count(),
                AtRiskCount = atRiskTickets.Count(),
                BreachedCount = breachedTickets.Count(),
                ServerNowUtc = nowUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        return summary ?? new TicketSlaSummaryResponse { ServerNowUtc = nowUtc };
    }
}
