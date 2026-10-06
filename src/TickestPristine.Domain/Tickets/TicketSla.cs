namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Política de SLA de resolução: prazo em horas corridas por prioridade, contado da abertura. O chamado entra em
/// risco ao consumir <see cref="WarningRatio"/> da janela. Os prazos são gravados no chamado na abertura, então mudar
/// a política não altera os chamados que já existem; se a prioridade de um chamado passar a ser editável, os prazos
/// devem ser recalculados a partir do <see cref="Ticket.CreatedAtUtc"/> com a nova prioridade.
/// </summary>
public static class TicketSla
{
    public const double WarningRatio = 0.75;

    public static TimeSpan ResolutionTime(TicketPriority priority) =>
        priority switch
        {
            TicketPriority.Urgent => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(8),
            TicketPriority.Medium => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(72)
        };

    public static DateTime DueAt(DateTime createdAtUtc, TicketPriority priority) =>
        createdAtUtc + ResolutionTime(priority);

    public static DateTime WarningAt(DateTime createdAtUtc, TicketPriority priority) =>
        createdAtUtc + ResolutionTime(priority) * WarningRatio;

    /// <summary>
    /// Situação do SLA no instante informado. A primeira resolução decide o resultado (cumprido ou violado);
    /// chamado cancelado sem ter sido resolvido fica fora do SLA.
    /// </summary>
    public static TicketSlaStatus GetStatus(
        TicketStatus status,
        DateTime dueAtUtc,
        DateTime slaWarningAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc)
    {
        if (resolvedAtUtc is { } resolvedAt)
        {
            return resolvedAt <= dueAtUtc ? TicketSlaStatus.Met : TicketSlaStatus.Missed;
        }

        if (status == TicketStatus.Canceled)
        {
            return TicketSlaStatus.NotApplicable;
        }

        if (nowUtc >= dueAtUtc)
        {
            return TicketSlaStatus.Breached;
        }

        return nowUtc >= slaWarningAtUtc ? TicketSlaStatus.AtRisk : TicketSlaStatus.OnTime;
    }
}
