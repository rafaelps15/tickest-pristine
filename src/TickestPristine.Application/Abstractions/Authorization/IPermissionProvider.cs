namespace TickestPristine.Application.Abstractions.Authorization;

/// <summary>
/// Permissões efetivas e situação do usuário; usuário inativo não tem permissões efetivas. A leitura em cache serve à
/// autorização; as leituras sem cache servem às decisões que gravam. Depois de mudar as funções, as permissões ou a
/// situação de alguém, chame <c>InvalidateAsync</c> após o commit.
/// </summary>
public interface IPermissionProvider
{
    Task<HashSet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// As mesmas permissões efetivas, lidas agora do banco, sem o cache: vazio para usuário inativo ou inexistente.
    /// </summary>
    Task<HashSet<string>> GetCurrentForUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permissões que as funções do usuário concedem, lidas agora do banco, mesmo com o usuário inativo.
    /// </summary>
    Task<HashSet<string>> GetGrantedByRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);

    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
