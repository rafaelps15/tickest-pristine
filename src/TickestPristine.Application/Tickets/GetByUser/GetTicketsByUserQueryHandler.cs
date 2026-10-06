using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetByUser;

internal sealed class GetTicketsByUserQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketsByUserQuery, PagedResponse<TicketResponse>>
{
    public async Task<Result<PagedResponse<TicketResponse>>> Handle(GetTicketsByUserQuery query, CancellationToken cancellationToken)
    {
        bool isSelf = query.UserId == userContext.UserId;
        string requiredPermission = isSelf ? PermissionCodes.Tickets.ViewOwn : PermissionCodes.Tickets.Manage;

        bool hasPermission = await permissionProvider.HasPermissionAsync(userContext.UserId, requiredPermission, cancellationToken);

        if (!hasPermission)
        {
            return Result.Failure<PagedResponse<TicketResponse>>(UserErrors.Unauthorized());
        }

        PagedResponse<TicketResponse> tickets = await context.Tickets
            .Where(t => t.CreatedByUserId == query.UserId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Select(t => new TicketResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                OpenedByUserId = t.CreatedByUserId,
                AssignedToUserId = t.AssignedToUserId,
                CreatedAtUtc = t.CreatedAtUtc
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return tickets;
    }
}
