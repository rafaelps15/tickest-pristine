using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetStatusCounts;

/// <summary>
/// Totais atuais de todos os chamados da empresa, não excluídos, por status, para os cards do dashboard. São só números
/// agregados, por isso atendem qualquer usuário ativo (spec 031); os do próprio usuário ficam em "Meus chamados".
/// </summary>
public sealed record GetTicketStatusCountsQuery : IQuery<TicketStatusCountsResponse>;
