namespace TickestPristine.Application.Abstractions.Authorization;

public interface IPermissionProvider
{
    Task<HashSet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
