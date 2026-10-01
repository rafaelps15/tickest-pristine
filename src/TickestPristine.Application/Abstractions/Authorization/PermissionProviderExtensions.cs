namespace TickestPristine.Application.Abstractions.Authorization;

public static class PermissionProviderExtensions
{
    /// <summary>
    /// Retorna verdadeiro quando o usuário age sobre si mesmo ou possui a permissão informada.
    /// </summary>
    public static async Task<bool> IsSelfOrHasPermissionAsync(
        this IPermissionProvider permissionProvider,
        Guid callerId,
        Guid targetUserId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (callerId == targetUserId)
        {
            return true;
        }

        return await permissionProvider.HasPermissionAsync(callerId, permission, cancellationToken);
    }
}
