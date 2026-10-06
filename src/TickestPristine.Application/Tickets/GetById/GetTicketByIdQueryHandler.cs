using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetById;

internal sealed class GetTicketByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketByIdQuery, TicketResponse>
{
    public async Task<Result<TicketResponse>> Handle(GetTicketByIdQuery query, CancellationToken cancellationToken)
    {
        DateTime nowUtc = dateTimeProvider.UtcNow;

        TicketResponse? ticket = await context.Tickets
            .Where(t => t.Id == query.TicketId)
            .Select(t => new TicketResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                OpenedByUserId = t.CreatedByUserId,
                OpenedByName = context.Users
                    .Where(u => u.Id == t.CreatedByUserId)
                    .Select(u => u.FirstName + " " + u.LastName)
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
                SlaStatus = TicketSla.GetStatus(t.Status, t.DueAtUtc, t.SlaWarningAtUtc, t.ResolvedAtUtc, nowUtc)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketResponse>(TicketErrors.NotFound(query.TicketId));
        }

        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            permissionProvider,
            userContext.UserId,
            ticket.OpenedByUserId,
            ticket.AssignedToUserId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<TicketResponse>(access.Error);
        }

        return ticket;
    }
}
