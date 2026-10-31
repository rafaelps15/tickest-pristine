using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Plano de SLA aplicado ao chamado, gravado na abertura e nunca recalculado por mudança na política. As metas são
/// minutos úteis do calendário do plano; sem meta de primeira resposta, o chamado não tem esse cronômetro (caso dos
/// chamados anteriores ao plano, marcados como legado).
/// </summary>
public sealed record TicketSlaPlan(
    Guid? PolicyId,
    string PolicyName,
    int PolicyVersion,
    Guid CalendarId,
    int? FirstResponseMinutes,
    int ResolutionMinutes,
    bool IsLegacy)
{
    // O objetivo entra em risco ao consumir 75% da meta em tempo útil.
    private const double WarningRatio = 0.75;

    /// <summary>
    /// Prazo e alerta do objetivo para um ciclo iniciado em <paramref name="startedAtUtc"/>, no tempo útil do
    /// calendário; devolve <c>null</c> quando o plano não tem meta para o objetivo. Lança
    /// <see cref="InvalidOperationException"/> quando o calendário não tem tempo útil suficiente.
    /// </summary>
    public SlaDeadlines? GetDeadlines(SlaMetric metric, BusinessCalendar calendar, DateTime startedAtUtc)
    {
        if (GetTargetMinutes(metric) is not { } minutes)
        {
            return null;
        }

        // Em ticks, para o alerta cair no segundo exato (ex.: 75% de 30 minutos = 22 minutos e 30 segundos).
        var target = TimeSpan.FromTicks(TimeSpan.TicksPerMinute * minutes);
        var warning = TimeSpan.FromTicks((long)(target.Ticks * WarningRatio));

        return new SlaDeadlines(
            calendar.AddBusinessTime(startedAtUtc, warning),
            calendar.AddBusinessTime(startedAtUtc, target));
    }

    /// <summary>
    /// Meta do objetivo em minutos úteis; nula quando o plano não tem meta para ele (primeira resposta do plano legado).
    /// </summary>
    internal int? GetTargetMinutes(SlaMetric metric) => metric switch
    {
        SlaMetric.FirstResponse => FirstResponseMinutes,
        SlaMetric.Resolution => ResolutionMinutes,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null)
    };

    /// <summary>
    /// Plano que o chamado recebe na abertura, da política "Padrão" (única e definida em código): metas de primeira
    /// resposta e de resolução em minutos úteis do calendário "Expediente". O plano fica gravado no chamado, então mudar
    /// as metas aqui só vale para os chamados abertos depois.
    /// </summary>
    public static TicketSlaPlan Default(TicketPriority priority)
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
            "Padrão",
            PolicyVersion: 1,
            SlaCalendarIds.Default,
            firstResponseMinutes,
            resolutionMinutes,
            IsLegacy: false);
    }

    /// <summary>
    /// Plano dos chamados abertos antes do plano de SLA: só a meta de resolução, nas horas corridas da prioridade, no
    /// calendário "24 horas". É o mesmo plano que a migration Add_Ticket_Sla_Timers grava nos chamados existentes.
    /// </summary>
    public static TicketSlaPlan Legacy(TicketPriority priority)
    {
        int resolutionHours = priority switch
        {
            TicketPriority.Urgent => 4,
            TicketPriority.High => 8,
            TicketPriority.Medium => 24,
            _ => 72
        };

        return new TicketSlaPlan(
            PolicyId: null,
            "Legado (horas corridas)",
            PolicyVersion: 1,
            SlaCalendarIds.Continuous,
            FirstResponseMinutes: null,
            resolutionHours * 60,
            IsLegacy: true);
    }
}
