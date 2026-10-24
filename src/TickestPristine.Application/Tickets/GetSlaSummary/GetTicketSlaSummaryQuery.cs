using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetSlaSummary;

/// <summary>
/// Totais do SLA dos chamados ativos (Aberto e Em andamento), opcionalmente de um setor, para o topo do painel.
/// </summary>
public sealed record GetTicketSlaSummaryQuery(Guid? SectorId) : IQuery<TicketSlaSummaryResponse>;
