using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetVolume;

/// <summary>
/// O endpoint exige só usuário ativo: com filtro de recorte, a permissão <c>tickets:manage</c> é conferida aqui, para a
/// falta dela responder 403 com mensagem de negócio (spec 031, DEC-2).
/// </summary>
internal sealed class GetTicketVolumeQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetTicketVolumeQuery, List<TicketVolumeResponse>>
{
    private const string Created = "Created";
    private const string Resolved = "Resolved";

    // Criados pela data de abertura e resolvidos pela data de cada mudança de status para Resolvido no histórico, os dois
    // agrupados pelo horário de Brasília (o date_trunc começa a semana na segunda-feira). O recorte vale para os dois,
    // e o filtro de excluídos do recorte também tira do gráfico as resoluções dos chamados excluídos (spec 028, R-1).
    private const string Sql =
        $"""
         SELECT '{Created}' AS {nameof(VolumeRow.Kind)},
                date_trunc(@Unit::text, t.created_at_utc AT TIME ZONE 'America/Sao_Paulo')::date AS {nameof(VolumeRow.PeriodStart)},
                COUNT(*)::int AS {nameof(VolumeRow.Count)}
         FROM public.tickets t
         WHERE {TicketScopeFilter.SqlCondition}
           AND t.created_at_utc >= @RangeStartUtc AND t.created_at_utc < @RangeEndUtc
         GROUP BY 2
         UNION ALL
         SELECT '{Resolved}',
                date_trunc(@Unit::text, h.occurred_at_utc AT TIME ZONE 'America/Sao_Paulo')::date,
                COUNT(*)::int
         FROM public.ticket_histories h
         JOIN public.tickets t ON t.id = h.ticket_id
         WHERE {TicketScopeFilter.SqlCondition}
           AND h.action = '{nameof(TicketHistoryAction.StatusChanged)}'
           AND h.new_value = '{nameof(TicketStatus.Resolved)}'
           AND h.occurred_at_utc >= @RangeStartUtc AND h.occurred_at_utc < @RangeEndUtc
         GROUP BY 2
         """;

    public async Task<Result<List<TicketVolumeResponse>>> Handle(GetTicketVolumeQuery query, CancellationToken cancellationToken)
    {
        DateOnly today = LocalDatePeriod.ToLocalDate(dateTimeProvider.UtcNow);
        Result<IReadOnlyList<DateOnly>> periods = TicketVolumePeriods.Build(query.GroupBy, query.From, query.To, today);

        if (periods.IsFailure)
        {
            return Result.Failure<List<TicketVolumeResponse>>(periods.Error);
        }

        if (query.Scope.HasSlicingFilter)
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

        DateOnly firstPeriod = periods.Value[0];
        DateOnly afterLastPeriod = TicketVolumePeriods.Step(query.GroupBy, periods.Value[^1], 1);
        var range = LocalDatePeriod.FromLocalDates(firstPeriod, afterLastPeriod.AddDays(-1));

        // Os períodos do recorte não se aplicam: o intervalo do gráfico é o de From e To.
        TicketScopeFilter scope = query.Scope with { CreatedFrom = null, CreatedTo = null, ResolvedFrom = null, ResolvedTo = null };
        DynamicParameters parameters = scope.ToSqlParameters();
        parameters.Add("Unit", TicketVolumePeriods.SqlUnit(query.GroupBy));
        parameters.Add("RangeStartUtc", range.FromUtc);
        parameters.Add("RangeEndUtc", range.UntilUtc);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        List<VolumeRow> rows = (await connection.QueryAsync<VolumeRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        var created = rows.Where(r => r.Kind == Created).ToDictionary(r => r.PeriodStart, r => r.Count);
        var resolved = rows.Where(r => r.Kind == Resolved).ToDictionary(r => r.PeriodStart, r => r.Count);

        return periods.Value
            .Select(period => new TicketVolumeResponse
            {
                PeriodStart = period,
                Count = created.GetValueOrDefault(period),
                ResolvedCount = resolved.GetValueOrDefault(period)
            })
            .ToList();
    }

    /// <summary>
    /// Linha do SQL: o tipo da contagem (criados ou resolvidos), o início do período e a quantidade.
    /// </summary>
    internal sealed class VolumeRow
    {
        public string Kind { get; init; }
        public DateOnly PeriodStart { get; init; }
        public int Count { get; init; }
    }
}
