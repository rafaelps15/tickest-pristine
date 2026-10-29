using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.GetStatusCounts;

/// <summary>
/// Totais atuais de todos os usuários por situação, para o card de resumo da tela de usuários. Não aplica filtros: conta
/// todos os usuários, por isso o endpoint exige a permissão de leitura de usuários.
/// </summary>
public sealed record GetUserStatusCountsQuery : IQuery<UserStatusCountsResponse>;
