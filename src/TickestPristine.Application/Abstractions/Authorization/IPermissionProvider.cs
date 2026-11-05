namespace TickestPristine.Application.Abstractions.Authorization;

/// <summary>
/// Permissões efetivas e situação do usuário, em cache; usuário inativo não tem permissões. Depois de mudar as
/// funções, as permissões ou a situação de alguém, chame <c>InvalidateAsync</c> após o commit.
/// </summary>
public interface IPermissionProvider
{
    Task<HashSet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);

    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
