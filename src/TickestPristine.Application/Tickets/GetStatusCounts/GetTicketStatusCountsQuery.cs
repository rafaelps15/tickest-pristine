using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetStatusCounts;

/// <summary>
/// Totais atuais de todos os chamados da empresa, não excluídos, por status, para os cards do dashboard. Não filtra
/// por quem abriu: os chamados do próprio usuário ficam em "Meus chamados".
/// </summary>
public sealed record GetTicketStatusCountsQuery : IQuery<TicketStatusCountsResponse>;
