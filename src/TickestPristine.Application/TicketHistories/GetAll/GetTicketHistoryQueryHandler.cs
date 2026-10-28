using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketHistories.GetAll;

internal sealed class GetTicketHistoryQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketHistoryQuery, CursorPagedResponse<TicketHistoryResponse>>
{
    public async Task<Result<CursorPagedResponse<TicketHistoryResponse>>> Handle(
        GetTicketHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            context,
            permissionProvider,
            userContext.UserId,
            query.TicketId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<CursorPagedResponse<TicketHistoryResponse>>(access.Error);
        }

        // Nos registros de responsável, os valores são ids de usuário gravados como texto: os nomes saem de
        // subconsultas na mesma consulta da página (inclusive de usuários desativados), só nessas linhas.
        CursorPagedResponse<TicketHistoryResponse> history = await context.TicketHistories
            .Where(h => h.TicketId == query.TicketId)
            .AfterCursor(query.Paging, h => h.OccurredAtUtc, h => h.Id)
            .Select(h => new TicketHistoryResponse
            {
                Id = h.Id,
                TicketId = h.TicketId,
                ChangedByUserId = h.ChangedByUserId,
                Action = h.Action,
                Description = TicketHistoryDescriptions.For(
                    h.Action,
                    h.OldValue,
                    h.NewValue,
                    h.Action == TicketHistoryAction.Reassigned ||
                    h.Action == TicketHistoryAction.Unassigned ||
                    h.Action == TicketHistoryAction.UnassignedOnUserDeactivation
                        ? context.Users
                            .Where(u => u.Id.ToString() == h.OldValue)
                            .Select(u => u.FirstName + " " + u.LastName)
                            .FirstOrDefault()
                        : null,
                    h.Action == TicketHistoryAction.Assigned || h.Action == TicketHistoryAction.Reassigned
                        ? context.Users
                            .Where(u => u.Id.ToString() == h.NewValue)
                            .Select(u => u.FirstName + " " + u.LastName)
                            .FirstOrDefault()
                        : null),
                OldValue = h.OldValue,
                NewValue = h.NewValue,
                OccurredAtUtc = h.OccurredAtUtc
            })
            .ToCursorPagedListAsync(query.Paging, h => h.OccurredAtUtc, h => h.Id, cancellationToken);

        return history;
    }
}
