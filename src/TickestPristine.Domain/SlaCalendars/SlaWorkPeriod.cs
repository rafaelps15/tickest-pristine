namespace TickestPristine.Domain.SlaCalendars;

/// <summary>
/// Uma faixa de expediente num dia da semana, em hora local do fuso do calendário. Um dia pode ter várias faixas ou
/// nenhuma.
/// </summary>
public sealed record SlaWorkPeriod(DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);
