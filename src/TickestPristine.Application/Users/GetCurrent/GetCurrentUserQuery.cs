using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.GetCurrent;

/// <summary>
/// Dados do usuário logado, com as funções e as permissões que ele tem agora.
/// </summary>
public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;
