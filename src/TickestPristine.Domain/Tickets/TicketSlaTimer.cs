using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Cronômetro de um objetivo do SLA num ciclo do chamado. A reabertura cria um novo ciclo de resolução; início,
/// conclusão, parada e violação, uma vez gravados, não mudam. O <see cref="NextActionOnUtc"/> é o próximo instante em
/// que o monitoramento precisa agir sobre o cronômetro; nulo quando não há mais nada a fazer. A
/// <see cref="CompletionCause"/> diz o que concluiu o cronômetro e só existe quando ele foi concluído (spec 027).
/// </summary>
public sealed class TicketSlaTimer : Entity, IAuditable
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public SlaMetric Metric { get; set; }
    public int Cycle { get; set; }
    public int TargetMinutes { get; set; }
    public Guid CalendarId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime WarningOnUtc { get; set; }
    public DateTime DueOnUtc { get; set; }
    public DateTime? CompletedOnUtc { get; set; }
    public SlaCompletionCause? CompletionCause { get; set; }
    public DateTime? StoppedOnUtc { get; set; }
    public DateTime? BreachedOnUtc { get; set; }
    public DateTime? NextActionOnUtc { get; set; }
}
