using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.ResendInvitation;

/// <summary>
/// Emite um novo convite de ativação para um usuário pendente, revogando os anteriores, e o envia por e-mail.
/// </summary>
public sealed record ResendUserInvitationCommand(Guid UserId) : ICommand;
