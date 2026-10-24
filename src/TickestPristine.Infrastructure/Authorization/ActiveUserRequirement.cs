using Microsoft.AspNetCore.Authorization;

namespace TickestPristine.Infrastructure.Authorization;

/// <summary>
/// Exige que o usuário do token continue ativo. Faz parte da policy padrão (<c>RequireAuthorization()</c>): sem ele,
/// um usuário desativado seguiria usando as rotas sem permissão própria até o access token expirar.
/// </summary>
internal sealed class ActiveUserRequirement : IAuthorizationRequirement;
