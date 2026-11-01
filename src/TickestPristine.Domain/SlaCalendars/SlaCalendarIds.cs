namespace TickestPristine.Domain.SlaCalendars;

/// <summary>
/// Ids reservados dos calendários definidos em código. Nenhum calendário do banco pode usar estes Ids (DEC-5 da spec
/// 004).
/// </summary>
public static class SlaCalendarIds
{
    public static readonly Guid Default = new("00000000-0000-0000-0000-000000000001");

    // Calendário "24 horas" dos chamados legados; a migration Add_Ticket_Sla_Timers grava este mesmo Id.
    public static readonly Guid Continuous = new("00000000-0000-0000-0000-000000000002");
}
