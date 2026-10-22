using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetStatusCounts;

/// <summary>
/// Totais atuais de todos os chamados da empresa, não excluídos, por status, para os cards do dashboard. Não filtra
/// por quem abriu, por isso o endpoint exige a permissão de gestão dos chamados; os do próprio usuário ficam em
/// "Meus chamados".
/// </summary>
public sealed record GetTicketStatusCountsQuery : IQuery<TicketStatusCountsResponse>;
