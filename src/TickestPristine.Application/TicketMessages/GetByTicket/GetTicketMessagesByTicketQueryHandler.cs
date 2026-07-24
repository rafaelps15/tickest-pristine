using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.GetByTicket;

internal sealed class GetTicketMessagesByTicketQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketMessagesByTicketQuery, List<TicketMessageResponse>>
{
    public async Task<Result<List<TicketMessageResponse>>> Handle(
        GetTicketMessagesByTicketQuery query,
        CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == query.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<List<TicketMessageResponse>>(TicketErrors.NotFound(query.TicketId));
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
                return Result.Failure<List<TicketMessageResponse>>(TicketErrors.Unauthorized());
            }
        }

        List<TicketMessageResponse> messages = await context.TicketMessages
            .Where(m => m.TicketId == query.TicketId)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new TicketMessageResponse
            {
                Id = m.Id,
                TicketId = m.TicketId,
                AuthorUserId = m.AuthorUserId,
                Content = m.Content,
                CreatedAtUtc = m.CreatedAtUtc,
                EditedAtUtc = m.EditedAtUtc
            })
            .ToListAsync(cancellationToken);

        return messages;
    }
}
