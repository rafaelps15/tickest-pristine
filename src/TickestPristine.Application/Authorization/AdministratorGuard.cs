using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Authorization;

internal static class AdministratorGuard
{
    /// <summary>
    /// Confere se, sem o usuário informado, ainda resta outro administrador ativo. Se restar, incrementa a versão da
    /// role de administrador para que duas operações simultâneas não deixem o sistema sem administrador.
    /// </summary>
    public static async Task<Result> EnsureAnotherActiveAdministratorAsync(
        IApplicationDbContext context,
        Role administratorRole,
        Guid leavingUserId,
        CancellationToken cancellationToken)
    {
        bool anotherActiveAdministratorExists = await context.UserRoles.AnyAsync(
            userRole => userRole.RoleId == administratorRole.Id &&
                        userRole.UserId != leavingUserId &&
                        context.Users.Any(user => user.Id == userRole.UserId && user.IsActive),
            cancellationToken);

        if (!anotherActiveAdministratorExists)
        {
            return Result.Failure(RoleErrors.LastAdministrator);
        }

        administratorRole.Version++;

        return Result.Success();
    }
}
