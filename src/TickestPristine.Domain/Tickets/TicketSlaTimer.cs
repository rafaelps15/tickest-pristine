using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Cronômetro de um objetivo do SLA num ciclo do chamado. A reabertura cria um novo ciclo de resolução; início,
/// conclusão e parada, uma vez gravados, não mudam.
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
    public DateTime? StoppedOnUtc { get; set; }
}
