namespace TickestPristine.Application.SlaCalendars;

/// <summary>
/// Feriados nacionais do Brasil de um ano: os fixos e os móveis calculados a partir da Páscoa. Carnaval e Corpus
/// Christi são pontos facultativos nacionais (<see cref="HolidayKind.OptionalDayOff"/>), mas entram aqui porque o
/// calendário padrão os trata como dia sem expediente (R-1 e DEC-17 da spec 004).
/// </summary>
public static class BrazilianNationalHolidays
{
    // O algoritmo da Páscoa só vale no calendário gregoriano, adotado em 1582 (DEC-18 da spec 004).
    private const int FirstSupportedYear = 1583;

    // Lei 14.759/2023: o 20/11 passou a ser feriado nacional em 2024.
    private const int BlackConsciousnessDayFirstYear = 2024;

    public static IReadOnlyList<BrazilianHoliday> For(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, FirstSupportedYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, DateOnly.MaxValue.Year);

        DateOnly easter = EasterSunday(year);

        List<BrazilianHoliday> holidays =
        [
            new(new DateOnly(year, 1, 1), "Confraternização Universal", HolidayKind.National),
            new(easter.AddDays(-48), "Carnaval (segunda-feira)", HolidayKind.OptionalDayOff),
            new(easter.AddDays(-47), "Carnaval (terça-feira)", HolidayKind.OptionalDayOff),
            new(easter.AddDays(-2), "Sexta-feira Santa", HolidayKind.National),
            new(new DateOnly(year, 4, 21), "Tiradentes", HolidayKind.National),
            new(new DateOnly(year, 5, 1), "Dia do Trabalhador", HolidayKind.National),
            new(easter.AddDays(60), "Corpus Christi", HolidayKind.OptionalDayOff),
            new(new DateOnly(year, 9, 7), "Independência do Brasil", HolidayKind.National),
            new(new DateOnly(year, 10, 12), "Nossa Senhora Aparecida", HolidayKind.National),
            new(new DateOnly(year, 11, 2), "Finados", HolidayKind.National),
            new(new DateOnly(year, 11, 15), "Proclamação da República", HolidayKind.National),
            new(new DateOnly(year, 12, 25), "Natal", HolidayKind.National)
        ];

        if (year >= BlackConsciousnessDayFirstYear)
        {
            holidays.Add(new(new DateOnly(year, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra", HolidayKind.National));
        }

        // A Sexta-feira Santa pode cair depois de Tiradentes (Páscoa em 24 ou 25/04), então a ordem não é fixa.
        return [.. holidays.OrderBy(h => h.Date)];
    }

    // Domingo de Páscoa no calendário gregoriano, pelo algoritmo de Meeus/Jones/Butcher.
    private static DateOnly EasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = (h + l - 7 * m + 114) % 31 + 1;

        return new DateOnly(year, month, day);
    }
}
