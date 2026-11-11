using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Permissions;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Application.Tickets.GetVolume;

/// <summary>
/// O endpoint exige só usuário ativo: com filtro de recorte, a permissão <c>tickets:manage</c> é conferida aqui, para a
/// falta dela responder 403 com mensagem de negócio.
/// </summary>
internal sealed class GetTicketVolumeQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketVolumeQuery, List<TicketVolumeResponse>>
{
    public async Task<Result<List<TicketVolumeResponse>>> Handle(
        GetTicketVolumeQuery request,
        CancellationToken cancellationToken)
    {
        DateOnly today = LocalDatePeriod.ToLocalDate(dateTimeProvider.UtcNow);
        Result<IReadOnlyList<DateOnly>> periods = TicketVolumePeriods.Build(request.GroupBy, request.From, request.To, today);

        if (periods.IsFailure)
        {
            return Result.Failure<List<TicketVolumeResponse>>(periods.Error);
        }

        if (request.Scope.HasSlicingFilter)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<List<TicketVolumeResponse>>(TicketErrors.StatsScopeForbidden);
            }
        }

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Criados pela data de abertura e resolvidos pela data de cada mudança de status para Resolvido no histórico, os
        // dois agrupados pelo horário de Brasília (o date_trunc começa a semana na segunda-feira) e casados com cada
        // período pedido: o período sem nenhum chamado sai com zero. O recorte vale para os dois, e o filtro de excluídos
        // do recorte também tira do gráfico as resoluções dos chamados excluídos.
        const string sql =
            $"""
             WITH created AS (
                 SELECT date_trunc(@Unit::text, t.created_at_utc AT TIME ZONE 'America/Sao_Paulo')::date AS period_start,
                        COUNT(*)::int AS total
                 FROM public.tickets t
                 WHERE {TicketSql.MatchesScope}
                   AND t.created_at_utc >= @RangeStartUtc AND t.created_at_utc < @RangeEndUtc
                 GROUP BY 1),
             resolved AS (
                 SELECT date_trunc(@Unit::text, h.occurred_at_utc AT TIME ZONE 'America/Sao_Paulo')::date AS period_start,
                        COUNT(*)::int AS total
                 FROM public.ticket_histories h
                 JOIN public.tickets t ON t.id = h.ticket_id
                 WHERE {TicketSql.MatchesScope}
                   AND h.action = '{nameof(TicketHistoryAction.StatusChanged)}'
                   AND h.new_value = '{nameof(TicketStatus.Resolved)}'
                   AND h.occurred_at_utc >= @RangeStartUtc AND h.occurred_at_utc < @RangeEndUtc
                 GROUP BY 1)
             SELECT
                 p.period_start AS {nameof(TicketVolumeResponse.PeriodStart)},
                 COALESCE(created.total, 0) AS {nameof(TicketVolumeResponse.Count)},
                 COALESCE(resolved.total, 0) AS {nameof(TicketVolumeResponse.ResolvedCount)}
             FROM unnest(@Periods::date[]) AS p(period_start)
             LEFT JOIN created ON created.period_start = p.period_start
             LEFT JOIN resolved ON resolved.period_start = p.period_start
             ORDER BY p.period_start
             """;

        DateOnly firstPeriod = periods.Value[0];
        DateOnly afterLastPeriod = TicketVolumePeriods.Step(request.GroupBy, periods.Value[^1], 1);
        var range = LocalDatePeriod.FromLocalDates(firstPeriod, afterLastPeriod.AddDays(-1));

        // Os períodos do recorte não se aplicam: o intervalo do gráfico é o de From e To.
        List<TicketVolumeResponse> volume = (await connection.QueryAsync<TicketVolumeResponse>(
            new CommandDefinition(
                sql,
                new
                {
                    SectorIds = request.Scope.SectorIds.ToArray(),
                    request.Scope.DepartmentId,
                    Priority = request.Scope.Priority?.ToString(),
                    request.Scope.RequesterId,
                    request.Scope.UnassignedOnly,
                    request.Scope.AssignedToUserId,
                    CreatedFromUtc = (DateTime?)null,
                    CreatedUntilUtc = (DateTime?)null,
                    ResolvedFromUtc = (DateTime?)null,
                    ResolvedUntilUtc = (DateTime?)null,
                    Unit = TicketVolumePeriods.SqlUnit(request.GroupBy),
                    RangeStartUtc = range.FromUtc,
                    RangeEndUtc = range.UntilUtc,
                    Periods = periods.Value.ToArray()
                },
                cancellationToken: cancellationToken))).AsList();

        return volume;
    }
}

