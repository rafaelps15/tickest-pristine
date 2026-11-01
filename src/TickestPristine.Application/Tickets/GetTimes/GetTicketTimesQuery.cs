using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetTimes;

/// <summary>
/// Tempos médios de primeira resposta e de resolução, em minutos úteis, no <see cref="Scope"/> da listagem. O período
/// de resolução do recorte (<c>resolvedFrom</c>/<c>resolvedTo</c>) vale para a data da conclusão de cada tempo: a
/// primeira resposta ou a primeira resolução no período (spec 027, DEC-4).
/// </summary>
public sealed record GetTicketTimesQuery(TicketScopeFilter Scope) : IQuery<TicketTimesResponse>;
