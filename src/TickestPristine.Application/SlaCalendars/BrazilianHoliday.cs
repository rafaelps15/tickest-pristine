namespace TickestPristine.Application.SlaCalendars;

/// <summary>
/// Uma data de <see cref="BrazilianNationalHolidays"/>, com o nome em pt-BR e a classificação.
/// </summary>
public sealed record BrazilianHoliday(DateOnly Date, string Name, HolidayKind Kind);
