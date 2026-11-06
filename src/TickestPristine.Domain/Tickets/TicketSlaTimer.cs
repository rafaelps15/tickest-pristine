using TickestPristine.Domain.SlaCalendars;
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Cronômetro de um objetivo do SLA num ciclo do chamado. A reabertura cria um novo ciclo de resolução; início,
/// conclusão, parada e violação, uma vez gravados, não mudam. O <see cref="NextActionOnUtc"/> é o próximo instante em
/// que o monitoramento precisa agir sobre o cronômetro; nulo quando não há mais nada a fazer. A
/// <see cref="CompletionCause"/> diz o que concluiu o cronômetro e só existe quando ele foi concluído.
/// </summary>
public sealed class TicketSlaTimer : Entity, IAuditable
{
    private TicketSlaTimer()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public SlaMetric Metric { get; private set; }

    public int Cycle { get; private set; }

    public int TargetMinutes { get; private set; }

    public Guid CalendarId { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime WarningOnUtc { get; private set; }

    public DateTime DueOnUtc { get; private set; }

    public DateTime? CompletedOnUtc { get; private set; }

    public SlaCompletionCause? CompletionCause { get; private set; }

    public DateTime? StoppedOnUtc { get; private set; }

    public DateTime? BreachedOnUtc { get; private set; }

    public DateTime? NextActionOnUtc { get; private set; }

    public bool IsOpen => CompletedOnUtc is null && StoppedOnUtc is null;

    internal bool IsQueued => NextActionOnUtc is not null;

    /// <summary>
    /// Cria o cronômetro com a meta, o calendário e o prazo e o alerta já calculados pelo
    /// <see cref="TicketSlaPlan.CalculateDeadlines"/>, na fila do monitoramento até o prazo; chamado só pelo
    /// <see cref="TicketSlaCycleService"/>, na abertura e na reabertura.
    /// </summary>
    internal static TicketSlaTimer Create(
        Guid ticketId,
        SlaMetric metric,
        int cycle,
        int targetMinutes,
        Guid calendarId,
        DateTime startedAtUtc,
        SlaDeadlines deadlines) =>
        new()
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Metric = metric,
            Cycle = cycle,
            TargetMinutes = targetMinutes,
            CalendarId = calendarId,
            StartedAtUtc = startedAtUtc,
            WarningOnUtc = deadlines.WarningOnUtc,
            DueOnUtc = deadlines.DueOnUtc,
            NextActionOnUtc = deadlines.DueOnUtc
        };

    /// <summary>
    /// Conclui o cronômetro em aberto, gravando o que o concluiu; devolve <c>false</c> quando ele já estava concluído ou
    /// parado. Concluído até o prazo, sai da fila do monitoramento; depois do prazo, continua nela, para o monitoramento
    /// gravar a violação.
    /// </summary>
    internal bool Complete(DateTime completedAtUtc, SlaCompletionCause cause)
    {
        if (!IsOpen)
        {
            return false;
        }

        CompletedOnUtc = completedAtUtc;
        CompletionCause = cause;

        if (completedAtUtc <= DueOnUtc)
        {
            NextActionOnUtc = null;
        }

        return true;
    }

    /// <summary>
    /// Conclui a primeira resposta do chamado com a mensagem de quem atende, no instante dela. Devolve <c>false</c>, sem
    /// mudar nada, quando o cronômetro não é a primeira resposta deste chamado, já está concluído ou parado, o autor é o
    /// solicitante ou não atende chamados.
    /// </summary>
    public bool CompleteFirstResponse(Ticket ticket, Guid authorUserId, bool authorIsAgent, DateTime utcNow)
    {
        if (Metric != SlaMetric.FirstResponse ||
            !BelongsTo(ticket) ||
            authorUserId == ticket.CreatedByUserId ||
            !authorIsAgent)
        {
            return false;
        }

        return Complete(utcNow, SlaCompletionCause.Message);
    }

    /// <summary>
    /// Para o cronômetro em aberto e o tira da fila (parado não conta como violação); devolve <c>false</c> quando ele já
    /// estava concluído ou parado.
    /// </summary>
    internal bool Stop(DateTime stoppedAtUtc)
    {
        if (!IsOpen)
        {
            return false;
        }

        StoppedOnUtc = stoppedAtUtc;
        NextActionOnUtc = null;

        return true;
    }

    /// <summary>
    /// Tira o cronômetro da fila do monitoramento sem violação, evento nem parada (exclusão do chamado).
    /// </summary>
    internal void Dequeue() => NextActionOnUtc = null;

    /// <summary>
    /// Ação do monitoramento sobre um cronômetro da fila: o não cumprido (em aberto, ou concluído depois do prazo) tem a
    /// violação gravada no próprio prazo e dispara o evento; o concluído até o prazo e o parado só saem da fila. Devolve
    /// <c>false</c> quando o cronômetro não foi violado.
    /// </summary>
    public bool ProcessDue()
    {
        NextActionOnUtc = null;

        bool metDeadline = CompletedOnUtc is { } completedAt && completedAt <= DueOnUtc;

        if (metDeadline || StoppedOnUtc is not null || BreachedOnUtc is not null)
        {
            return false;
        }

        BreachedOnUtc = DueOnUtc;

        Raise(new TicketSlaBreachedDomainEvent(TicketId, Metric, Cycle, DueOnUtc));

        return true;
    }

    /// <summary>
    /// Estado no instante informado: em aberto, em andamento antes do alerta, em risco do alerta até o prazo e violado
    /// a partir do prazo; concluído até o prazo (inclusive), cumprido; depois dele, perdido; parado, parado.
    /// </summary>
    public SlaObjectiveState StateAt(DateTime utcNow)
    {
        if (CompletedOnUtc is { } completedAt)
        {
            return completedAt <= DueOnUtc ? SlaObjectiveState.Met : SlaObjectiveState.Missed;
        }

        if (StoppedOnUtc is not null)
        {
            return SlaObjectiveState.Stopped;
        }

        if (utcNow >= DueOnUtc)
        {
            return SlaObjectiveState.Breached;
        }

        return utcNow >= WarningOnUtc ? SlaObjectiveState.AtRisk : SlaObjectiveState.Running;
    }

    internal bool BelongsTo(Ticket ticket) => TicketId == ticket.Id;

    /// <summary>
    /// Tempo útil consumido entre o início e o menor instante entre agora, a conclusão e a parada.
    /// </summary>
    public TimeSpan ConsumedBusinessTime(BusinessCalendar calendar, DateTime utcNow)
    {
        DateTime endUtc = new[] { utcNow, CompletedOnUtc ?? DateTime.MaxValue, StoppedOnUtc ?? DateTime.MaxValue }.Min();

        return calendar.BusinessTimeBetween(StartedAtUtc, endUtc);
    }
}
