using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Recorte de chamados comum à listagem (<c>GET tickets</c>) e às estatísticas: setores (vazio é todos), departamento
/// (todos os setores dele, ativos ou não), prioridade, solicitante, responsável, só sem responsável (que, quando ligado,
/// ignora o responsável) e os períodos de abertura e de primeira resolução, em datas locais de São Paulo
/// (<see cref="LocalDatePeriod"/>). As consultas usam o trecho <see cref="TicketSql.MatchesScope"/>, com os parâmetros
/// no objeto de cada handler.
/// </summary>
public sealed record TicketScopeFilter(
    IReadOnlyCollection<Guid> SectorIds,
    Guid? DepartmentId,
    TicketPriority? Priority,
    Guid? RequesterId,
    Guid? AssignedToUserId,
    bool UnassignedOnly,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    DateOnly? ResolvedFrom,
    DateOnly? ResolvedTo)
{
    /// <summary>
    /// Início depois do fim em qualquer um dos dois períodos (400 <see cref="TicketErrors.InvalidPeriod"/>).
    /// </summary>
    public bool HasInvertedPeriod =>
        LocalDatePeriod.IsInverted(CreatedFrom, CreatedTo) || LocalDatePeriod.IsInverted(ResolvedFrom, ResolvedTo);

    /// <summary>
    /// Algum filtro de recorte além dos períodos: setores, departamento, prioridade, solicitante, responsável ou só sem
    /// responsável. Os períodos não contam, porque o período da empresa toda não expõe setor nem pessoa.
    /// </summary>
    public bool HasSlicingFilter =>
        SectorIds.Count > 0 || DepartmentId != null || Priority != null || RequesterId != null || AssignedToUserId != null ||
        UnassignedOnly;
}
