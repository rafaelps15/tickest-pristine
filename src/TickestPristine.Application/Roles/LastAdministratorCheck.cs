using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Roles;

namespace TickestPristine.Application.Roles;

/// <summary>
/// Conferência do último administrador, que também protege a concorrência entre duas remoções simultâneas.
/// </summary>
internal sealed class LastAdministratorCheck(IApplicationDbContext context)
{
    /// <summary>
    /// Chamado antes de um usuário perder o acesso (desativação ou remoção da role): verdadeiro quando ele é
    /// administrador e não resta outro com conta utilizável, ativa e com a senha já definida. Para quem não é
    /// administrador, falso.
    /// </summary>
    /// <remarks>
    /// Quando resta outro administrador, trava a role de administrador pelo token de concorrência, que o SaveChanges
    /// confere: de duas operações simultâneas que tirariam os dois últimos administradores, só a primeira consegue salvar.
    /// </remarks>
    public async Task<bool> IsLastAdministratorWithLockAsync(Guid leavingUserId, CancellationToken cancellationToken)
    {
        string administratorRoleName = RoleNames.Administrator;

        bool isAdministrator = await context.UserRoles.AnyAsync(
            userRole => userRole.UserId == leavingUserId && userRole.RoleName == administratorRoleName,
            cancellationToken);

        if (!isAdministrator)
        {
            return false;
        }

        // A role é lida antes da checagem: o valor original do token passa a ser o do momento da decisão, então uma
        // remoção concorrente que salvar primeiro faz esta falhar no SaveChanges (409), em vez de passar as duas.
        Role administratorRole = await context.Roles.SingleAsync(r => r.Name == administratorRoleName, cancellationToken);

        // A conta pendente (ativa, sem senha) não conta: ela só entra no sistema depois de aceitar o convite.
        bool anotherUsableAdministratorExists = await context.UserRoles.AnyAsync(
            userRole => userRole.RoleName == administratorRoleName &&
                        userRole.UserId != leavingUserId &&
                        context.Users.Any(user => user.Id == userRole.UserId && user.IsActive) &&
                        context.UserCredentials.Any(credential => credential.UserId == userRole.UserId),
            cancellationToken);

        if (!anotherUsableAdministratorExists)
        {
            return true;
        }

        context.Entry(administratorRole).Property(role => role.Version).CurrentValue++;

        return false;
    }
}
