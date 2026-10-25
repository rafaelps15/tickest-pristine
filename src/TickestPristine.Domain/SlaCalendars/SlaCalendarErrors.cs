using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.SlaCalendars;

public static class SlaCalendarErrors
{
    public static Error NotFound(Guid slaCalendarId) => Error.NotFound(
        "SlaCalendars.NotFound",
        $"O calendário de SLA com o Id = '{slaCalendarId}' não foi encontrado");
}
