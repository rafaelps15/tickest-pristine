namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Plano de SLA aplicado ao chamado, gravado na abertura e nunca recalculado por mudança na política. As metas são
/// minutos úteis do calendário do plano; sem meta de primeira resposta, o chamado não tem esse cronômetro (caso dos
/// chamados anteriores ao plano, marcados como legado).
/// </summary>
public sealed record TicketSlaPlan(
    Guid? PolicyId,
    string PolicyName,
    int PolicyVersion,
    Guid CalendarId,
    int? FirstResponseMinutes,
    int ResolutionMinutes,
    bool IsLegacy);
