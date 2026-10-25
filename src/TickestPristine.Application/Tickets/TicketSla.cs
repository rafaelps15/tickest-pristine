using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Planos de SLA e a situação do SLA de resolução gravada no chamado (<see cref="Ticket.DueAtUtc"/> e
/// <see cref="Ticket.SlaWarningAtUtc"/>, sempre os do primeiro ciclo de resolução). A política "Padrão" é única e
/// definida em código; o plano legado é o dos chamados abertos antes dela, com o prazo em horas corridas por
/// prioridade, contado da abertura.
/// </summary>
public static class TicketSla
{
    public const double WarningRatio = 0.75;

    public const string DefaultPolicyName = "Padrão";

    public const int DefaultPolicyVersion = 1;

    public const string LegacyPolicyName = "Legado (horas corridas)";

    /// <summary>
    /// Plano que o chamado recebe na abertura: metas de primeira resposta e de resolução em minutos úteis do calendário
    /// "Expediente". O plano fica gravado no chamado, então mudar as metas aqui só vale para os chamados abertos depois.
    /// </summary>
    public static TicketSlaPlan DefaultPlan(TicketPriority priority)
    {
        (int firstResponseMinutes, int resolutionMinutes) = priority switch
        {
            TicketPriority.Urgent => (30, 240),
            TicketPriority.High => (120, 600),
            TicketPriority.Medium => (240, 1200),
            _ => (600, 3000)
        };

        return new TicketSlaPlan(
            PolicyId: null,
            DefaultPolicyName,
            DefaultPolicyVersion,
            SlaCalendarIds.Default,
            firstResponseMinutes,
            resolutionMinutes,
            IsLegacy: false);
    }

    /// <summary>
    /// Plano dos chamados abertos antes do plano de SLA: só a meta de resolução, nas horas corridas da prioridade, no
    /// calendário "24 horas". É o mesmo plano que a migration Add_Ticket_Sla_Timers grava nos chamados existentes.
    /// </summary>
    public static TicketSlaPlan LegacyPlan(TicketPriority priority) => new(
        PolicyId: null,
        LegacyPolicyName,
        PolicyVersion: 1,
        SlaCalendarIds.Continuous,
        FirstResponseMinutes: null,
        (int)ResolutionTime(priority).TotalMinutes,
        IsLegacy: true);

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
