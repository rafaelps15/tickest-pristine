using System.Data.Common;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Application.TicketHistories.GetAll;

internal sealed class GetTicketHistoryQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    TicketViewAccess ticketViewAccess)
    : IQueryHandler<GetTicketHistoryQuery, CursorPagedResponse<TicketHistoryResponse>>
{
    public async Task<Result<CursorPagedResponse<TicketHistoryResponse>>> Handle(
        GetTicketHistoryQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        Result access = await ticketViewAccess.EnsureCanViewAsync(connection, userContext.UserId, request.TicketId, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<CursorPagedResponse<TicketHistoryResponse>>(access.Error);
        }

        // Nos registros de responsável, os valores são ids de usuário gravados como texto: os nomes saem de subconsultas
        // na mesma consulta da página (inclusive de usuários desativados), só nessas linhas. O segundo objeto começa pela
        // chave do registro, que nunca é nula, para o Dapper montá-lo também quando não há nome.
        const string sql =
            $"""
             SELECT
                 h.id AS {nameof(TicketHistoryResponse.Id)},
                 h.ticket_id AS {nameof(TicketHistoryResponse.TicketId)},
                 h.changed_by_user_id AS {nameof(TicketHistoryResponse.ChangedByUserId)},
                 h.action AS {nameof(TicketHistoryResponse.Action)},
                 h.old_value AS {nameof(TicketHistoryResponse.OldValue)},
                 h.new_value AS {nameof(TicketHistoryResponse.NewValue)},
                 h.occurred_at_utc AS {nameof(TicketHistoryResponse.OccurredAtUtc)},
                 h.id AS {nameof(AssigneeNames.HistoryId)},
                 CASE WHEN h.action IN ('{nameof(TicketHistoryAction.Reassigned)}', '{nameof(TicketHistoryAction.Unassigned)}') THEN (
                     SELECT u.first_name || ' ' || u.last_name
                     FROM public.users u
                     WHERE u.id::text = h.old_value)
                 END AS {nameof(AssigneeNames.PreviousName)},
                 CASE WHEN h.action IN ('{nameof(TicketHistoryAction.Assigned)}', '{nameof(TicketHistoryAction.Reassigned)}') THEN (
                     SELECT u.first_name || ' ' || u.last_name
                     FROM public.users u
                     WHERE u.id::text = h.new_value)
                 END AS {nameof(AssigneeNames.NewName)}
             FROM public.ticket_histories h
             WHERE h.ticket_id = @TicketId
               AND (@CursorDate::timestamptz IS NULL OR (h.occurred_at_utc, h.id) < (@CursorDate::timestamptz, @CursorId::uuid))
             ORDER BY h.occurred_at_utc DESC, h.id DESC
             LIMIT @Limit
             """;

        // A descrição não sai do SQL: o texto depende da ação e, nos registros de responsável, dos nomes lidos.
        CursorPagedResponse<TicketHistoryResponse> history =
            await connection.QueryCursorPagedAsync<TicketHistoryResponse, AssigneeNames, TicketHistoryResponse>(
                sql,
                new { request.TicketId },
                request.Paging,
                (item, names) =>
                {
                    item.Description = TicketHistoryEntryDescription.For(
                        item.Action,
                        item.OldValue,
                        item.NewValue,
                        names.PreviousName,
                        names.NewName);

                    return item;
                },
                nameof(AssigneeNames.HistoryId),
                item => item.OccurredAtUtc,
                item => item.Id,
                cancellationToken);

        return history;
    }

    internal sealed class AssigneeNames
    {
        public Guid HistoryId { get; init; }
        public string? PreviousName { get; init; }
        public string? NewName { get; init; }
    }
}
