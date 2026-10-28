using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetOwn;

internal sealed class GetOwnTicketsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetOwnTicketsQuery, PagedResponse<OwnTicketResponse>>
{
    private static readonly TicketStatus[] ActiveStatuses = [TicketStatus.Open, TicketStatus.InProgress];

    private static readonly TicketStatus[] FinishedStatuses =
        [TicketStatus.Resolved, TicketStatus.Closed, TicketStatus.Canceled];

    public async Task<Result<PagedResponse<OwnTicketResponse>>> Handle(GetOwnTicketsQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;
        DateTime nowUtc = dateTimeProvider.UtcNow;

        // Uma consulta de permissão por requisição, e não por item: todos os chamados da lista são de quem consulta.
        bool canCancelAny = await permissionProvider.HasPermissionAsync(userId, PermissionCodes.Tickets.Cancel, cancellationToken);
        bool canEditOwn = await permissionProvider.HasPermissionAsync(userId, PermissionCodes.Tickets.UpdateOwn, cancellationToken);
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        TicketStatus[]? statuses = query.StatusGroup switch
        {
            OwnTicketStatusGroup.Active => ActiveStatuses,
            OwnTicketStatusGroup.Finished => FinishedStatuses,
            _ => null
        };

        // O título é citext: a busca não diferencia maiúsculas, como na listagem geral.
        PagedResponse<OwnTicketResponse> tickets = await context.Tickets
            .Where(t =>
                t.CreatedByUserId == userId &&
                (statuses == null || statuses.Contains(t.Status)) &&
                (search == null || t.Title.Contains(search)))
            .OrderByDescending(t => t.LastActivityAtUtc)
            .ThenByDescending(t => t.Id)
            .Select(t => new OwnTicketResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                OpenedByUserId = t.CreatedByUserId,
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
                // Mesma regra do Domain e mesmo instante da listagem geral: a situação é calculada na projeção final.
                SlaStatus = TicketSla.GetStatus(t.Status, t.DueAtUtc, t.SlaWarningAtUtc, t.ResolvedAtUtc, nowUtc),
                LastActivityAtUtc = t.LastActivityAtUtc,
                CanCancel = TicketCancellationPolicy.CanCancel(t.Status, t.CreatedAtUtc, nowUtc, canCancelAny, canEditOwn),
                OwnerCancelDeadlineUtc = TicketCancellationPolicy.OwnerDeadline(t.CreatedAtUtc)
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return tickets;
    }
}
