using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// As mesmas regras de <see cref="TicketSla.GetStatus"/>, escritas como filtros que o EF traduz para SQL, para a
/// listagem filtrar e o resumo contar no banco. Os testes do painel conferem que as duas versões concordam.
/// </summary>
internal static class TicketSlaFilters
{
    /// <summary>
    /// Chamados ativos (Aberto e Em andamento), a mesma regra de <see cref="TicketStatusTransitions.IsActive"/>.
    /// </summary>
    internal static IQueryable<Ticket> WhereActive(this IQueryable<Ticket> tickets) =>
        tickets.Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);

    /// <summary>
    /// Mantém só os chamados na situação de SLA informada; sem situação, devolve a consulta como está.
    /// </summary>
    internal static IQueryable<Ticket> WhereSlaStatus(this IQueryable<Ticket> tickets, TicketSlaStatus? slaStatus, DateTime nowUtc) =>
        slaStatus switch
        {
            null => tickets,
            TicketSlaStatus.Met => tickets.Where(t => t.ResolvedAtUtc != null && t.ResolvedAtUtc <= t.DueAtUtc),
            TicketSlaStatus.Missed => tickets.Where(t => t.ResolvedAtUtc != null && t.ResolvedAtUtc > t.DueAtUtc),
            TicketSlaStatus.NotApplicable => tickets.Where(t => t.ResolvedAtUtc == null && t.Status == TicketStatus.Canceled),
            TicketSlaStatus.Breached => tickets.WhereRunning().Where(t => nowUtc >= t.DueAtUtc),
            TicketSlaStatus.AtRisk => tickets.WhereRunning().Where(t => nowUtc >= t.SlaWarningAtUtc && nowUtc < t.DueAtUtc),
            _ => tickets.WhereRunning().Where(t => nowUtc < t.SlaWarningAtUtc)
        };

    /// <summary>
    /// Chamados com o relógio do SLA correndo: ainda sem resolução e não cancelados.
    /// </summary>
    private static IQueryable<Ticket> WhereRunning(this IQueryable<Ticket> tickets) =>
        tickets.Where(t => t.ResolvedAtUtc == null && t.Status != TicketStatus.Canceled);
}
