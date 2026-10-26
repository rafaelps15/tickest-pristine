using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.ChangeSector;

/// <summary>
/// Define o setor do usuário (o endpoint exige <c>users:update</c>). Só aceita setor ativo; desativar o setor depois
/// não apaga o vínculo. Informar o setor atual não muda nada.
/// </summary>
internal sealed class ChangeUserSectorCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : ICommandHandler<ChangeUserSectorCommand>
{
    public async Task<Result> Handle(ChangeUserSectorCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        Result privilegeCheck = await PrivilegeEscalationGuard.EnsureCallerCanManageUserAsync(
            context,
            permissionProvider,
            userContext.UserId,
            user.Id,
            cancellationToken);

        if (privilegeCheck.IsFailure)
        {
            return privilegeCheck;
        }

        if (command.SectorId is Guid sectorId)
        {
            bool sectorIsActive = await context.Sectors.AnyAsync(s => s.Id == sectorId && s.IsActive, cancellationToken);

            if (!sectorIsActive)
            {
                return Result.Failure(SectorErrors.NotFound(sectorId));
            }
        }

        if (user.SectorId == command.SectorId)
        {
            return Result.Success();
        }

        Guid? previousSectorId = user.SectorId;

        user.SectorId = command.SectorId;

        user.Raise(new UserSectorChangedDomainEvent(user.Id, previousSectorId, command.SectorId));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
