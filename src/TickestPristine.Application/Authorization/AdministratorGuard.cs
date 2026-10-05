using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Authorization;

internal static class AdministratorGuard
{
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
        bool isAdministrator = await context.UserRoles.AnyAsync(
            userRole => userRole.UserId == leavingUserId && userRole.RoleId == SystemRoles.AdministratorId,
            cancellationToken);

        if (!isAdministrator)
        {
            return Result.Success();
        }

        bool anotherActiveAdministratorExists = await context.UserRoles.AnyAsync(
            userRole => userRole.RoleId == SystemRoles.AdministratorId &&
                        userRole.UserId != leavingUserId &&
                        context.Users.Any(user => user.Id == userRole.UserId && user.IsActive),
            cancellationToken);

        if (!anotherActiveAdministratorExists)
        {
            return Result.Failure(RoleErrors.LastAdministrator);
        }

        Role administratorRole = await context.Roles.SingleAsync(r => r.Id == SystemRoles.AdministratorId, cancellationToken);

        administratorRole.Version++;

        return Result.Success();
    }
}
