namespace TickestPristine.Application.Abstractions.Authentication;

public interface IUserContext
{
    Guid UserId { get; }

    /// <summary>
    /// Id do usuário autenticado, ou nulo fora de uma requisição autenticada (login, cadastro, seed).
    /// </summary>
    Guid? UserIdOrDefault { get; }
}
