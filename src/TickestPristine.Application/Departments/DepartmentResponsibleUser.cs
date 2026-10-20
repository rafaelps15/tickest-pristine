using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments;

/// <summary>
/// Regra do responsável pelo departamento, igual na criação e na edição.
/// </summary>
internal static class DepartmentResponsibleUser
{
    /// <summary>
    /// Sem responsável informado, passa direto; informado, o usuário precisa existir.
    /// </summary>
    public static async Task<Result> EnsureExistsAsync(
        IApplicationDbContext context,
        Guid? responsibleUserId,
        CancellationToken cancellationToken)
    {
        if (responsibleUserId is not { } userId)
        {
            return Result.Success();
        }

        bool exists = await context.Users.AnyAsync(u => u.Id == userId, cancellationToken);

        return exists ? Result.Success() : Result.Failure(UserErrors.NotFound(userId));
    }
}
