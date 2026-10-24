using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetOwnSummary;

/// <summary>
/// Quantidade de chamados abertos pelo usuário logado em cada combinação de prioridade e status, para o dashboard, que
/// não pode contar a partir da listagem paginada. Só vêm as combinações que têm chamados.
/// </summary>
public sealed record GetOwnTicketSummaryQuery : IQuery<List<OwnTicketCountResponse>>;
