using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Notifications;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.ResendInvitation;

/// <summary>
/// Reenvia o convite de ativação (o endpoint exige <c>users:create</c>). Só vale para usuário ativo que ainda não definiu
/// a senha, e no máximo <see cref="UserInvitations.MaxIssuedInWindow"/> convites em
/// <see cref="UserInvitations.LimitWindow"/>.
/// </summary>
internal sealed class ResendUserInvitationCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IEmailSender emailSender,
    IActivationLinkBuilder activationLinkBuilder,
    IDateTimeProvider dateTimeProvider,
    ILogger<ResendUserInvitationCommandHandler> logger)
    : ICommandHandler<ResendUserInvitationCommand>
{
    public async Task<Result> Handle(ResendUserInvitationCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        if (!user.IsActive)
        {
            return Result.Failure(UserErrors.InvitationUserDeactivated);
        }

        if (await context.UserCredentials.AnyAsync(c => c.UserId == user.Id, cancellationToken))
        {
            return Result.Failure(UserErrors.AlreadyActivated);
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

        DateTime nowUtc = dateTimeProvider.UtcNow;

        if (await UserInvitations.HasReachedLimitAsync(context, user.Id, nowUtc, cancellationToken))
        {
            return Result.Failure(UserErrors.InvitationLimitReached(
                UserInvitations.MaxIssuedInWindow,
                (int)UserInvitations.LimitWindow.TotalHours));
        }

        IssuedInvitation invitation = await UserInvitations.IssueAsync(context, user.Id, nowUtc, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await UserInvitations.SendAsync(emailSender, activationLinkBuilder, logger, user, invitation, cancellationToken);

        return Result.Success();
    }
}
