using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetAll;

internal sealed class GetTicketsQueryHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketsQuery, PagedResponse<TicketSummaryResponse>>
{
    public async Task<Result<PagedResponse<TicketSummaryResponse>>> Handle(GetTicketsQuery query, CancellationToken cancellationToken)
    {
        if (LocalDatePeriod.IsInverted(query.CreatedFrom, query.CreatedTo) ||
            LocalDatePeriod.IsInverted(query.ResolvedFrom, query.ResolvedTo))
        {
            return Result.Failure<PagedResponse<TicketSummaryResponse>>(TicketErrors.InvalidPeriod);
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        bool allStatuses = query.Statuses.Count == 0;
        bool allSectors = query.SectorIds.Count == 0;
        var created = LocalDatePeriod.FromLocalDates(query.CreatedFrom, query.CreatedTo);
        var resolved = LocalDatePeriod.FromLocalDates(query.ResolvedFrom, query.ResolvedTo);
        DateTime? createdFromUtc = created.FromUtc;
        DateTime? createdUntilUtc = created.UntilUtc;
        DateTime? resolvedFromUtc = resolved.FromUtc;
        DateTime? resolvedUntilUtc = resolved.UntilUtc;
        bool unassignedOnly = query.UnassignedOnly;
        Guid? assignedToUserId = unassignedOnly ? null : query.AssignedToUserId;

        IQueryable<Ticket> filtered = context.Tickets
            .Where(t =>
                (allStatuses || query.Statuses.Contains(t.Status)) &&
                (query.Priority == null || t.Priority == query.Priority) &&
                (allSectors || query.SectorIds.Contains(t.SectorId)) &&
                (query.RequesterId == null || t.CreatedByUserId == query.RequesterId) &&
                (!unassignedOnly || t.AssignedToUserId == null) &&
                (assignedToUserId == null || t.AssignedToUserId == assignedToUserId) &&
                (search == null || t.Title.Contains(search)) &&
                (createdFromUtc == null || t.CreatedAtUtc >= createdFromUtc) &&
                (createdUntilUtc == null || t.CreatedAtUtc < createdUntilUtc) &&
                // Sem resolução, as comparações dão falso: o chamado nunca resolvido fica fora do período de resolução.
                (resolvedFromUtc == null || t.ResolvedAtUtc >= resolvedFromUtc) &&
                (resolvedUntilUtc == null || t.ResolvedAtUtc < resolvedUntilUtc))
            .WhereSlaStatus(query.SlaStatus, nowUtc);

        // O solicitante é obrigatório (chave estrangeira), então o join não descarta chamados; nome e e-mail saem da
        // mesma linha, em vez de uma subconsulta para cada um.
        var withRequester =
            from t in filtered
            join requester in context.Users on t.CreatedByUserId equals requester.Id
            select new { Ticket = t, Requester = requester };

        var ordered = query.Sort == TicketSortOrder.DueDate
            ? withRequester.OrderBy(x => x.Ticket.DueAtUtc).ThenBy(x => x.Ticket.Id)
            : withRequester.OrderByDescending(x => x.Ticket.CreatedAtUtc).ThenByDescending(x => x.Ticket.Id);

        PagedResponse<TicketSummaryResponse> tickets = await ordered
            .Select(x => new TicketSummaryResponse
            {
                Id = x.Ticket.Id,
                Title = x.Ticket.Title,
                Priority = x.Ticket.Priority,
                Status = x.Ticket.Status,
                RequesterId = x.Ticket.CreatedByUserId,
                RequesterName = x.Requester.FirstName + " " + x.Requester.LastName,
                RequesterEmail = x.Requester.Email,
                AssignedToUserId = x.Ticket.AssignedToUserId,
                AssignedToName = context.Users
                    .Where(u => u.Id == x.Ticket.AssignedToUserId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault(),
                SectorId = x.Ticket.SectorId,
                SectorName = context.Sectors
                    .Where(s => s.Id == x.Ticket.SectorId)
                    .Select(s => s.Name)
                    .FirstOrDefault()!,
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                SlaWarningAtUtc = x.Ticket.SlaWarningAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                // Regra do Domain: o EF lê as colunas e calcula a situação na projeção final.
                SlaStatus = TicketSla.GetStatus(
                    x.Ticket.Status,
                    x.Ticket.DueAtUtc,
                    x.Ticket.SlaWarningAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    nowUtc)
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return tickets;
    }
}
