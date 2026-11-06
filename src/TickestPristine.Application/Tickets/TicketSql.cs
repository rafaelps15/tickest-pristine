using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Regras de chamado escritas como trechos de SQL com nome, para as consultas em Dapper, sobre a tabela
/// <c>public.tickets</c> com o alias <c>t</c>. Cada trecho de regra espelha uma regra do <see cref="Ticket"/>, e um
/// teste de integração confere que os dois concordam; o recorte (<see cref="MatchesScope"/>) é o filtro comum à
/// listagem e às estatísticas. Os trechos da situação do SLA esperam o parâmetro <c>@NowUtc</c>.
/// </summary>
internal static class TicketSql
{
    /// <summary>
    /// Recorte comum à listagem e às estatísticas (<see cref="TicketScopeFilter"/>), já sem os chamados excluídos.
    /// Espera <c>@SectorIds</c> (vazio é todos), <c>@DepartmentId</c> (todos os setores dele, ativos ou não),
    /// <c>@Priority</c> (texto), <c>@RequesterId</c>, <c>@UnassignedOnly</c> (que, ligado, ignora o responsável),
    /// <c>@AssignedToUserId</c> e os períodos já em UTC: <c>@CreatedFromUtc</c>, <c>@CreatedUntilUtc</c>,
    /// <c>@ResolvedFromUtc</c> e <c>@ResolvedUntilUtc</c>. Os casts explícitos deixam o PostgreSQL saber o tipo de um
    /// parâmetro nulo; sem resolução, as comparações de período dão falso, e o chamado nunca resolvido fica fora do
    /// período de resolução.
    /// </summary>
    internal const string MatchesScope =
        """
        t.deleted_at_utc IS NULL
          AND (cardinality(@SectorIds) = 0 OR t.sector_id = ANY(@SectorIds))
          AND (@DepartmentId::uuid IS NULL
               OR t.sector_id IN (SELECT ds.id FROM public.sectors ds WHERE ds.department_id = @DepartmentId::uuid))
          AND (@Priority::text IS NULL OR t.priority = @Priority::text)
          AND (@RequesterId::uuid IS NULL OR t.created_by_user_id = @RequesterId::uuid)
          AND (NOT @UnassignedOnly OR t.assigned_to_user_id IS NULL)
          AND (@UnassignedOnly OR @AssignedToUserId::uuid IS NULL OR t.assigned_to_user_id = @AssignedToUserId::uuid)
          AND (@CreatedFromUtc::timestamptz IS NULL OR t.created_at_utc >= @CreatedFromUtc::timestamptz)
          AND (@CreatedUntilUtc::timestamptz IS NULL OR t.created_at_utc < @CreatedUntilUtc::timestamptz)
          AND (@ResolvedFromUtc::timestamptz IS NULL OR t.resolved_at_utc >= @ResolvedFromUtc::timestamptz)
          AND (@ResolvedUntilUtc::timestamptz IS NULL OR t.resolved_at_utc < @ResolvedUntilUtc::timestamptz)
        """;

    /// <summary>
    /// Chamado ativo (Aberto ou Em andamento), a regra de <see cref="Ticket.IsActive"/>.
    /// </summary>
    internal const string IsActive = $"t.status IN ('{nameof(TicketStatus.Open)}', '{nameof(TicketStatus.InProgress)}')";

    /// <summary>
    /// Relógio do SLA correndo: ainda sem resolução e não cancelado.
    /// </summary>
    internal const string IsSlaRunning = $"t.resolved_at_utc IS NULL AND t.status <> '{nameof(TicketStatus.Canceled)}'";

    /// <summary>
    /// Situação <see cref="TicketSlaStatus.OnTime"/> de <see cref="Ticket.GetSlaStatus"/>: relógio correndo, antes do alerta.
    /// </summary>
    internal const string IsSlaOnTime = $"{IsSlaRunning} AND @NowUtc::timestamptz < t.sla_warning_at_utc";

    /// <summary>
    /// Situação <see cref="TicketSlaStatus.AtRisk"/> de <see cref="Ticket.GetSlaStatus"/>: relógio correndo, do alerta até o
    /// prazo.
    /// </summary>
    internal const string IsSlaAtRisk =
        $"{IsSlaRunning} AND @NowUtc::timestamptz >= t.sla_warning_at_utc AND @NowUtc::timestamptz < t.due_at_utc";

    /// <summary>
    /// Situação <see cref="TicketSlaStatus.Breached"/> de <see cref="Ticket.GetSlaStatus"/>: relógio correndo, a partir do
    /// prazo.
    /// </summary>
    internal const string IsSlaBreached = $"{IsSlaRunning} AND @NowUtc::timestamptz >= t.due_at_utc";

    /// <summary>
    /// Situação <see cref="TicketSlaStatus.Met"/> de <see cref="Ticket.GetSlaStatus"/>: primeira resolução até o prazo.
    /// </summary>
    internal const string IsSlaMet = "t.resolved_at_utc IS NOT NULL AND t.resolved_at_utc <= t.due_at_utc";

    /// <summary>
    /// Situação <see cref="TicketSlaStatus.Missed"/> de <see cref="Ticket.GetSlaStatus"/>: primeira resolução depois do prazo.
    /// </summary>
    internal const string IsSlaMissed = "t.resolved_at_utc IS NOT NULL AND t.resolved_at_utc > t.due_at_utc";

    /// <summary>
    /// Situação <see cref="TicketSlaStatus.NotApplicable"/> de <see cref="Ticket.GetSlaStatus"/>: cancelado sem nunca ter
    /// sido resolvido.
    /// </summary>
    internal const string IsSlaNotApplicable = $"t.resolved_at_utc IS NULL AND t.status = '{nameof(TicketStatus.Canceled)}'";

    /// <summary>
    /// Situação do SLA de resolução pelo nome do <see cref="TicketSlaStatus"/>, a regra de <see cref="Ticket.GetSlaStatus"/>
    /// num <c>CASE</c>: vale a primeira situação que se aplica, na ordem da entidade.
    /// </summary>
    internal const string SlaStatus =
        $"""
         CASE
             WHEN {IsSlaMet} THEN '{nameof(TicketSlaStatus.Met)}'
             WHEN {IsSlaMissed} THEN '{nameof(TicketSlaStatus.Missed)}'
             WHEN {IsSlaNotApplicable} THEN '{nameof(TicketSlaStatus.NotApplicable)}'
             WHEN {IsSlaBreached} THEN '{nameof(TicketSlaStatus.Breached)}'
             WHEN {IsSlaAtRisk} THEN '{nameof(TicketSlaStatus.AtRisk)}'
             ELSE '{nameof(TicketSlaStatus.OnTime)}'
         END
         """;

    /// <summary>
    /// Último instante em que o dono ainda pode cancelar o chamado, a regra de <see cref="Ticket.OwnerCancelDeadline"/>;
    /// espera <c>@OwnerCancelWindowMinutes</c> (<see cref="Ticket.OwnerCancelWindowMinutes"/>).
    /// </summary>
    internal const string OwnerCancelDeadline = "t.created_at_utc + make_interval(mins => @OwnerCancelWindowMinutes)";

    /// <summary>
    /// Se quem consulta pode cancelar o chamado agora, a regra de <see cref="Ticket.CanBeCanceled"/>: chamado ativo, e quem
    /// pode cancelar qualquer chamado (<c>@CanCancelAny</c>) ou o dono (<c>@CallerId</c>) que pode editar o próprio
    /// (<c>@CanEditOwn</c>), até o <see cref="OwnerCancelDeadline"/>. Espera também <c>@NowUtc</c> e
    /// <c>@OwnerCancelWindowMinutes</c>.
    /// </summary>
    internal const string CanBeCanceled =
        $"""
         ({IsActive}
          AND (@CanCancelAny
               OR (t.created_by_user_id = @CallerId::uuid
                   AND @CanEditOwn
                   AND @NowUtc::timestamptz <= {OwnerCancelDeadline})))
         """;

    /// <summary>
    /// Cronômetro de resolução do ciclo vigente do chamado (o de maior ciclo: a reabertura cria um novo), como junção
    /// lateral com o alias <c>current_timer</c>; sem cronômetro de resolução (plano legado), as colunas vêm nulas.
    /// </summary>
    internal const string CurrentResolutionTimer =
        $"""
         LEFT JOIN LATERAL (
             SELECT tm.id, tm.due_on_utc, tm.warning_on_utc, tm.completed_on_utc, tm.stopped_on_utc
             FROM public.ticket_sla_timers tm
             WHERE tm.ticket_id = t.id AND tm.metric = '{nameof(SlaMetric.Resolution)}'
             ORDER BY tm.cycle DESC
             LIMIT 1) current_timer ON TRUE
         """;

    /// <summary>
    /// Situação do ciclo vigente, a regra de <see cref="TicketSlaTimer.GetState"/> já como <see cref="TicketSlaStatus"/>
    /// (parado vira NotApplicable), sobre a junção de <see cref="CurrentResolutionTimer"/>: vale a primeira regra que se
    /// aplica, e sem cronômetro a situação vem nula.
    /// </summary>
    internal const string CurrentResolutionSlaStatus =
        $"""
         CASE
             WHEN current_timer.id IS NULL THEN NULL
             WHEN current_timer.completed_on_utc <= current_timer.due_on_utc THEN '{nameof(TicketSlaStatus.Met)}'
             WHEN current_timer.completed_on_utc IS NOT NULL THEN '{nameof(TicketSlaStatus.Missed)}'
             WHEN current_timer.stopped_on_utc IS NOT NULL THEN '{nameof(TicketSlaStatus.NotApplicable)}'
             WHEN @NowUtc::timestamptz >= current_timer.due_on_utc THEN '{nameof(TicketSlaStatus.Breached)}'
             WHEN @NowUtc::timestamptz >= current_timer.warning_on_utc THEN '{nameof(TicketSlaStatus.AtRisk)}'
             ELSE '{nameof(TicketSlaStatus.OnTime)}'
         END
         """;

    /// <summary>
    /// Colunas do cronômetro de SLA, sobre <c>public.ticket_sla_timers</c> com o alias <c>tm</c>, nos nomes das
    /// propriedades do <see cref="TicketSlaTimer"/>, para as consultas que leem os cronômetros e usam as regras dele.
    /// </summary>
    internal const string SlaTimerColumns =
        $"""
         tm.id AS {nameof(TicketSlaTimer.Id)},
         tm.ticket_id AS {nameof(TicketSlaTimer.TicketId)},
         tm.metric AS {nameof(TicketSlaTimer.Metric)},
         tm.cycle AS {nameof(TicketSlaTimer.Cycle)},
         tm.target_minutes AS {nameof(TicketSlaTimer.TargetMinutes)},
         tm.calendar_id AS {nameof(TicketSlaTimer.CalendarId)},
         tm.started_at_utc AS {nameof(TicketSlaTimer.StartedAtUtc)},
         tm.warning_on_utc AS {nameof(TicketSlaTimer.WarningOnUtc)},
         tm.due_on_utc AS {nameof(TicketSlaTimer.DueOnUtc)},
         tm.completed_on_utc AS {nameof(TicketSlaTimer.CompletedOnUtc)},
         tm.completion_cause AS {nameof(TicketSlaTimer.CompletionCause)},
         tm.stopped_on_utc AS {nameof(TicketSlaTimer.StoppedOnUtc)},
         tm.breached_on_utc AS {nameof(TicketSlaTimer.BreachedOnUtc)},
         tm.next_action_on_utc AS {nameof(TicketSlaTimer.NextActionOnUtc)}
         """;
}
