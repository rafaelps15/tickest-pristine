using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetByUser;

internal sealed class GetTicketsByUserQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketsByUserQuery, List<TicketResponse>>
{
    public async Task<Result<List<TicketResponse>>> Handle(GetTicketsByUserQuery query, CancellationToken cancellationToken)
    {
        if (query.UserId != userContext.UserId)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<List<TicketResponse>>(UserErrors.Unauthorized());
            }
        }

        List<TicketResponse> tickets = await context.Tickets
            .Where(t => t.CreatedByUserId == query.UserId)
            .Select(t => new TicketResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                OpenedByUserId = t.CreatedByUserId,
                AssignedToUserId = t.AssignedToUserId
            })
            .ToListAsync(cancellationToken);

        return tickets;
    }
}
