using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetAll;

internal sealed class GetTicketsQueryHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketsQuery, PagedResponse<TicketSummaryResponse>>
{
    public async Task<Result<PagedResponse<TicketSummaryResponse>>> Handle(GetTicketsQuery query, CancellationToken cancellationToken)
    {
        DateTime nowUtc = dateTimeProvider.UtcNow;
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        bool onlyActive = query.Statuses.Count == 0;

        PagedResponse<TicketSummaryResponse> tickets = await context.Tickets
            .Where(t =>
                (onlyActive
                    ? t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress
                    : query.Statuses.Contains(t.Status)) &&
                (query.Priority == null || t.Priority == query.Priority) &&
                (query.SectorId == null || t.SectorId == query.SectorId) &&
                (search == null || t.Title.Contains(search)))
            .WhereSlaStatus(query.SlaStatus, nowUtc)
            .OrderBy(t => t.DueAtUtc)
            .ThenBy(t => t.Id)
            .Select(t => new TicketSummaryResponse
            {
                Id = t.Id,
                Title = t.Title,
                Priority = t.Priority,
                Status = t.Status,
                RequesterId = t.CreatedByUserId,
                RequesterName = context.Users
                    .Where(u => u.Id == t.CreatedByUserId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault()!,
                RequesterEmail = context.Users
                    .Where(u => u.Id == t.CreatedByUserId)
                    .Select(u => u.Email)
                    .FirstOrDefault()!,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToName = context.Users
                    .Where(u => u.Id == t.AssignedToUserId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault(),
                SectorId = t.SectorId,
                SectorName = context.Sectors
                    .Where(s => s.Id == t.SectorId)
                    .Select(s => s.Name)
                    .FirstOrDefault()!,
                CreatedAtUtc = t.CreatedAtUtc,
                DueAtUtc = t.DueAtUtc,
                SlaWarningAtUtc = t.SlaWarningAtUtc,
                ResolvedAtUtc = t.ResolvedAtUtc,
                // Regra do Domain: o EF lê as colunas e calcula a situação na projeção final.
                SlaStatus = TicketSla.GetStatus(t.Status, t.DueAtUtc, t.SlaWarningAtUtc, t.ResolvedAtUtc, nowUtc)
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return tickets;
    }
}
