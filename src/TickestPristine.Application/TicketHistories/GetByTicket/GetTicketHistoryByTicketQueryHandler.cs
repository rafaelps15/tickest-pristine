using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketHistories.GetByTicket;

internal sealed class GetTicketHistoryByTicketQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketHistoryByTicketQuery, List<TicketHistoryResponse>>
{
    public async Task<Result<List<TicketHistoryResponse>>> Handle(
        GetTicketHistoryByTicketQuery query,
        CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == query.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<List<TicketHistoryResponse>>(TicketErrors.NotFound(query.TicketId));
        }

        bool isParticipant = ticket.CreatedByUserId == userContext.UserId || ticket.AssignedToUserId == userContext.UserId;

        if (!isParticipant)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<List<TicketHistoryResponse>>(TicketErrors.Unauthorized());
            }
        }

        List<TicketHistoryResponse> history = await context.TicketHistories
            .Where(h => h.TicketId == query.TicketId)
            .OrderBy(h => h.OccurredAtUtc)
            .Select(h => new TicketHistoryResponse
            {
                Id = h.Id,
                TicketId = h.TicketId,
                ChangedByUserId = h.ChangedByUserId,
                Action = h.Action,
                Description = h.Description,
                OldValue = h.OldValue,
                NewValue = h.NewValue,
                OccurredAtUtc = h.OccurredAtUtc
            })
            .ToListAsync(cancellationToken);

        return history;
    }
}
