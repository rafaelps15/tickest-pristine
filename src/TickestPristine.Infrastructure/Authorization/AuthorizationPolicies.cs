namespace TickestPristine.Infrastructure.Authorization;

public static class AuthorizationPolicies
{
    /// <summary>
    /// Só exige o token válido, sem conferir se o usuário continua ativo: para as rotas que o usuário desativado ainda
    /// precisa usar (consultar o próprio cadastro e sair).
    /// </summary>
    public const string AuthenticatedUser = "AuthenticatedUser";
}
