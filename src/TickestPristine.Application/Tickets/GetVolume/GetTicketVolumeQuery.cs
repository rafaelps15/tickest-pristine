using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetVolume;

/// <summary>
/// Quantos chamados da empresa foram abertos em cada dia, semana ou mês recente, para o gráfico do dashboard. Não
/// filtra por quem abriu, por isso o endpoint exige a permissão de gestão dos chamados. Os períodos seguem o horário de Brasília e vêm do mais antigo para o atual, inclusive os
/// sem chamados, com zero.
/// </summary>
public sealed record GetTicketVolumeQuery(TicketVolumeGrouping GroupBy) : IQuery<List<TicketVolumeResponse>>;
