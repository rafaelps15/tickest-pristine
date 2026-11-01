using TickestPristine.SharedKernel;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.Abstractions.Sla;

public interface ISlaCalendarProvider
{
    /// <summary>
    /// Calendário pronto para os cálculos: os definidos em código para <see cref="SlaCalendarIds.Default"/> e
    /// <see cref="SlaCalendarIds.Continuous"/>, sem ir ao banco; os demais, do banco, com grade e feriados. Calendário inexistente devolve
    /// <c>SlaCalendars.NotFound</c>.
    /// </summary>
    Task<Result<BusinessCalendar>> GetAsync(Guid calendarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Faz a próxima leitura do calendário buscar de novo no banco.
    /// </summary>
    Task InvalidateAsync(Guid calendarId, CancellationToken cancellationToken = default);
}
