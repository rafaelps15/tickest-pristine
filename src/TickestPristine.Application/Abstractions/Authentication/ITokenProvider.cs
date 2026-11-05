using TickestPristine.Domain.Users;

namespace TickestPristine.Application.Abstractions.Authentication;

/// <summary>
/// Emite o access token (JWT) do usuário e gera os refresh tokens. Só a identidade vai no token: as permissões são
/// lidas no servidor a cada requisição.
/// </summary>
public interface ITokenProvider
{
    Task<string> CreateAsync(User user, CancellationToken cancellationToken = default);

    GeneratedRefreshToken GenerateRefreshToken();
}
