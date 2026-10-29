using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.GetVolume;

/// <summary>
/// Quantos chamados da empresa foram abertos e quantas resoluções houve (cada mudança de status para Resolvido, inclusive
/// depois de reabrir) em cada dia, semana ou mês, para o gráfico do dashboard. Não filtra por quem abriu, por isso o
/// endpoint exige a permissão de gestão dos chamados. O <see cref="Scope"/> recorta os chamados (setores, prioridade,
/// solicitante e responsável; os períodos dele não se aplicam) e <see cref="From"/>/<see cref="To"/> definem o intervalo
/// do gráfico. Os períodos seguem o horário de Brasília e vêm do mais antigo para o mais recente, inclusive os vazios,
/// com zero.
/// </summary>
public sealed record GetTicketVolumeQuery(
    TicketVolumeGrouping GroupBy,
    TicketScopeFilter Scope,
    DateOnly? From,
    DateOnly? To) : IQuery<List<TicketVolumeResponse>>;
