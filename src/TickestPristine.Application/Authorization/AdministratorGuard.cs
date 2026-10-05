using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Authorization;

internal static class AdministratorGuard
{
    /// <summary>
    /// Nome da propriedade sombra (só no banco) usada como token de concorrência da role de administrador.
    /// </summary>
    internal const string RoleVersionProperty = "Version";

    /// <summary>
    /// Chamado antes de um administrador perder o acesso (desativação ou remoção da role): falha se ele for o último
    /// administrador ativo. Usuário que não é administrador passa direto.
    /// </summary>
    /// <remarks>
    /// Quando resta outro administrador, incrementa a versão da role de administrador (token de concorrência): de duas
    /// operações simultâneas que tirariam os dois últimos administradores, só a primeira consegue salvar.
    /// </remarks>
    public static async Task<Result> EnsureNotLastAdministratorAsync(
        IApplicationDbContext context,
        Guid leavingUserId,
        CancellationToken cancellationToken)
    {
        string administratorRoleName = Role.Administrator.Name;

        bool isAdministrator = await context.UserRoles.AnyAsync(
            userRole => userRole.UserId == leavingUserId && userRole.RoleName == administratorRoleName,
            cancellationToken);

        if (!isAdministrator)
        {
            return Result.Success();
        }

        bool anotherActiveAdministratorExists = await context.UserRoles.AnyAsync(
            userRole => userRole.RoleName == administratorRoleName &&
                        userRole.UserId != leavingUserId &&
                        context.Users.Any(user => user.Id == userRole.UserId && user.IsActive),
            cancellationToken);

        if (!anotherActiveAdministratorExists)
        {
            return Result.Failure(RoleErrors.LastAdministrator);
        }

        Role administratorRole = await context.Roles.SingleAsync(r => r.Name == administratorRoleName, cancellationToken);

        context.Entry(administratorRole).Property<int>(RoleVersionProperty).CurrentValue++;

        return Result.Success();
    }
}
