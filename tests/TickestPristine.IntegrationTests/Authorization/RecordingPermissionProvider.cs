using TickestPristine.Application.Abstractions.Authorization;

namespace TickestPristine.IntegrationTests.Authorization;

// Provedor de permissões dos testes dos handlers: responde o que o teste define e guarda o token de cancelamento recebido.
internal sealed class RecordingPermissionProvider(IEnumerable<string> permissions, bool isActive) : IPermissionProvider
{
    private readonly HashSet<string> _permissions = [.. permissions];

    public int Calls { get; private set; }

    public CancellationToken? ReceivedToken { get; private set; }

    public Task<HashSet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Record(cancellationToken);

        return Task.FromResult<HashSet<string>>([.. _permissions]);
    }

    public Task<HashSet<string>> GetCurrentForUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetForUserIdAsync(userId, cancellationToken);

    public Task<HashSet<string>> GetGrantedByRolesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetForUserIdAsync(userId, cancellationToken);

    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        Record(cancellationToken);

        return Task.FromResult(_permissions.Contains(permission));
    }

    public Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Record(cancellationToken);

        return Task.FromResult(isActive);
    }

    public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private void Record(CancellationToken cancellationToken)
    {
        Calls++;
        ReceivedToken = cancellationToken;
    }
}
