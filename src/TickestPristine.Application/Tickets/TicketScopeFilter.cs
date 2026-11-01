using Dapper;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Recorte de chamados comum à listagem (<c>GET tickets</c>) e às estatísticas (specs 026 a 028): setores (vazio é
/// todos), prioridade, solicitante, responsável, só sem responsável (que, quando ligado, ignora o responsável) e os
/// períodos de abertura e de primeira resolução, em datas locais de São Paulo (<see cref="LocalDatePeriod"/>). O mesmo
/// recorte existe em EF (<see cref="Apply"/>), para a listagem, e em SQL (<see cref="SqlCondition"/> com
/// <see cref="ToSqlParameters"/>), para as estatísticas; os testes de paridade da spec 026 conferem que concordam.
/// </summary>
public sealed record TicketScopeFilter(
    IReadOnlyCollection<Guid> SectorIds,
    TicketPriority? Priority,
    Guid? RequesterId,
    Guid? AssignedToUserId,
    bool UnassignedOnly,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    DateOnly? ResolvedFrom,
    DateOnly? ResolvedTo)
{
    /// <summary>
    /// Condição SQL do recorte sobre a tabela <c>public.tickets</c> com o alias <c>t</c>, já sem os chamados excluídos
    /// (como o filtro global do EF). Os parâmetros vêm de <see cref="ToSqlParameters"/>; os casts explícitos deixam o
    /// PostgreSQL saber o tipo de um parâmetro nulo.
    /// </summary>
    internal const string SqlCondition =
        """
        t.deleted_at_utc IS NULL
          AND (cardinality(@SectorIds) = 0 OR t.sector_id = ANY(@SectorIds))
          AND (@Priority::text IS NULL OR t.priority = @Priority::text)
          AND (@RequesterId::uuid IS NULL OR t.created_by_user_id = @RequesterId::uuid)
          AND (NOT @UnassignedOnly OR t.assigned_to_user_id IS NULL)
          AND (@AssignedToUserId::uuid IS NULL OR t.assigned_to_user_id = @AssignedToUserId::uuid)
          AND (@CreatedFromUtc::timestamptz IS NULL OR t.created_at_utc >= @CreatedFromUtc::timestamptz)
          AND (@CreatedUntilUtc::timestamptz IS NULL OR t.created_at_utc < @CreatedUntilUtc::timestamptz)
          AND (@ResolvedFromUtc::timestamptz IS NULL OR t.resolved_at_utc >= @ResolvedFromUtc::timestamptz)
          AND (@ResolvedUntilUtc::timestamptz IS NULL OR t.resolved_at_utc < @ResolvedUntilUtc::timestamptz)
        """;

    /// <summary>
    /// Início depois do fim em qualquer um dos dois períodos (400 <see cref="TicketErrors.InvalidPeriod"/>).
    /// </summary>
    public bool HasInvertedPeriod =>
        LocalDatePeriod.IsInverted(CreatedFrom, CreatedTo) || LocalDatePeriod.IsInverted(ResolvedFrom, ResolvedTo);

    /// <summary>
    /// Algum filtro de recorte além dos períodos: setores, prioridade, solicitante, responsável ou só sem responsável.
    /// Os períodos não contam, porque o período da empresa toda não expõe setor nem pessoa (spec 031, DEC-3).
    /// </summary>
    public bool HasSlicingFilter =>
        SectorIds.Count > 0 || Priority != null || RequesterId != null || AssignedToUserId != null || UnassignedOnly;

    internal IQueryable<Ticket> Apply(IQueryable<Ticket> tickets)
    {
        bool allSectors = SectorIds.Count == 0;
        IReadOnlyCollection<Guid> sectorIds = SectorIds;
        TicketPriority? priority = Priority;
        Guid? requesterId = RequesterId;
        bool unassignedOnly = UnassignedOnly;
        Guid? assignedToUserId = unassignedOnly ? null : AssignedToUserId;
        var created = LocalDatePeriod.FromLocalDates(CreatedFrom, CreatedTo);
        var resolved = LocalDatePeriod.FromLocalDates(ResolvedFrom, ResolvedTo);
        DateTime? createdFromUtc = created.FromUtc;
        DateTime? createdUntilUtc = created.UntilUtc;
        DateTime? resolvedFromUtc = resolved.FromUtc;
        DateTime? resolvedUntilUtc = resolved.UntilUtc;

        return tickets.Where(t =>
            (allSectors || sectorIds.Contains(t.SectorId)) &&
            (priority == null || t.Priority == priority) &&
            (requesterId == null || t.CreatedByUserId == requesterId) &&
            (!unassignedOnly || t.AssignedToUserId == null) &&
            (assignedToUserId == null || t.AssignedToUserId == assignedToUserId) &&
            (createdFromUtc == null || t.CreatedAtUtc >= createdFromUtc) &&
            (createdUntilUtc == null || t.CreatedAtUtc < createdUntilUtc) &&
            // Sem resolução, as comparações dão falso: o chamado nunca resolvido fica fora do período de resolução.
            (resolvedFromUtc == null || t.ResolvedAtUtc >= resolvedFromUtc) &&
            (resolvedUntilUtc == null || t.ResolvedAtUtc < resolvedUntilUtc));
    }

    internal DynamicParameters ToSqlParameters()
    {
        var created = LocalDatePeriod.FromLocalDates(CreatedFrom, CreatedTo);
        var resolved = LocalDatePeriod.FromLocalDates(ResolvedFrom, ResolvedTo);

        // A prioridade é gravada pelo nome (ex.: "Urgent"), como na configuração do EF.
        return new DynamicParameters(new
        {
            SectorIds = SectorIds.ToArray(),
            Priority = Priority?.ToString(),
            RequesterId,
            UnassignedOnly,
            AssignedToUserId = UnassignedOnly ? null : AssignedToUserId,
            CreatedFromUtc = created.FromUtc,
            CreatedUntilUtc = created.UntilUtc,
            ResolvedFromUtc = resolved.FromUtc,
            ResolvedUntilUtc = resolved.UntilUtc
        });
    }
}
