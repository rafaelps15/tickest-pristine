namespace TickestPristine.Application.Abstractions.Authentication;

/// <summary>
/// Usuário da requisição atual, lido do token. <c>UserId</c> lança exceção fora de uma requisição autenticada; onde
/// ela pode não ter usuário, use <c>UserIdOrDefault</c>.
/// </summary>
public interface IUserContext
{
    Guid UserId { get; }

    /// <summary>
    /// Id do usuário autenticado, ou nulo fora de uma requisição autenticada (login, cadastro, seed).
    /// </summary>
    Guid? UserIdOrDefault { get; }
}
