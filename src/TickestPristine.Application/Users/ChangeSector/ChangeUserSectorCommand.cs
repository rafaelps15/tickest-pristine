using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.ChangeSector;

/// <summary>
/// Vincula o usuário ao setor em que ele atende; <see cref="SectorId"/> nulo remove o vínculo.
/// </summary>
public sealed record ChangeUserSectorCommand(Guid UserId, Guid? SectorId) : ICommand;
