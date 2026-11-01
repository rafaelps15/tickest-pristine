namespace TickestPristine.Domain.SlaCalendars;

/// <summary>
/// Classificação de uma data de <see cref="BrazilianNationalHolidays"/>. É só informativa: no calendário padrão,
/// feriado e ponto facultativo ficam igualmente fora do expediente (R-1 e DEC-17 da spec 004).
/// </summary>
public enum HolidayKind
{
    National,
    OptionalDayOff
}
