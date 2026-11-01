using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetOverview;

/// <summary>
/// Estatísticas da operação numa só resposta, para o Início: volume por status, situação de SLA e pontos de atenção,
/// todos no mesmo <see cref="Scope"/> da listagem (<c>GET tickets</c>). Não recorta por status, situação de SLA nem
/// busca: o recorte por status é o próprio bloco de volume.
/// </summary>
public sealed record GetTicketOverviewQuery(TicketScopeFilter Scope) : IQuery<TicketOverviewResponse>;
