using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetSummaryByUser;

internal sealed class GetTicketSummaryByUserQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketSummaryByUserQuery, List<TicketCountResponse>>
{
    public async Task<Result<List<TicketCountResponse>>> Handle(
        GetTicketSummaryByUserQuery query,
        CancellationToken cancellationToken)
    {
        // Mesma regra da listagem "meus chamados".
        bool isSelf = query.UserId == userContext.UserId;
        string requiredPermission = isSelf ? PermissionCodes.Tickets.ViewOwn : PermissionCodes.Tickets.Manage;

        bool hasPermission = await permissionProvider.HasPermissionAsync(userContext.UserId, requiredPermission, cancellationToken);

        if (!hasPermission)
        {
            return Result.Failure<List<TicketCountResponse>>(UserErrors.Unauthorized());
        }

        List<TicketCountResponse> counts = await context.Tickets
            .Where(t => t.CreatedByUserId == query.UserId)
            .GroupBy(t => new { t.Priority, t.Status })
            .Select(g => new TicketCountResponse
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
