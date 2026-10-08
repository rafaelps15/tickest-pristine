using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetOwn;

internal sealed class GetOwnTicketsQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetOwnTicketsQuery, PagedResponse<OwnTicketResponse>>
{
    public async Task<Result<PagedResponse<OwnTicketResponse>>> Handle(GetOwnTicketsQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        PagedResponse<OwnTicketResponse> tickets = await context.Tickets
            .Where(t => t.CreatedByUserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
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
                CreatedAtUtc = t.CreatedAtUtc
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return tickets;
    }
}
