using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.ActivateAccount;

/// <summary>
/// Ativa a conta pelo token do convite: o próprio usuário define a senha. Não abre sessão; o login vem em seguida.
/// </summary>
public sealed record ActivateUserAccountCommand(string Token, string NewPassword) : ICommand;
