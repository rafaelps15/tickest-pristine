namespace TickestPristine.Infrastructure.Authentication;

public sealed class UserContextUnavailableException : Exception
{
    public UserContextUnavailableException() : base("O contexto do usuário não está disponível.")
    {
    }
}
