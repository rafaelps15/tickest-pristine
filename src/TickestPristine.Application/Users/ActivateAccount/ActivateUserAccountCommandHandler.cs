using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.ActivateAccount;

/// <summary>
/// Cria a credencial do usuário a partir de um convite utilizável e marca o convite como usado. Toda recusa do token
/// (inexistente, expirado, usado, revogado, de usuário desativado ou já ativado) é a mesma falha, para a resposta não
/// revelar o motivo. Duas ativações simultâneas com o mesmo token não criam duas credenciais: o uso do convite é token
/// de concorrência e a credencial é única por usuário.
/// </summary>
internal sealed class ActivateUserAccountCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ActivateUserAccountCommand>
{
    public async Task<Result> Handle(ActivateUserAccountCommand command, CancellationToken cancellationToken)
    {
        string tokenHash = UserInvitations.HashToken(command.Token);
        DateTime nowUtc = dateTimeProvider.UtcNow;

        UserInvitation? invitation = await context.UserInvitations
            .SingleOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null ||
            invitation.UsedAtUtc is not null ||
            invitation.RevokedAtUtc is not null ||
            invitation.ExpiresAtUtc <= nowUtc)
        {
            return Result.Failure(UserErrors.InvalidActivationToken);
        }

        bool userIsActive = await context.Users.AnyAsync(u => u.Id == invitation.UserId && u.IsActive, cancellationToken);
        bool alreadyActivated = await context.UserCredentials.AnyAsync(c => c.UserId == invitation.UserId, cancellationToken);

        if (!userIsActive || alreadyActivated)
        {
            return Result.Failure(UserErrors.InvalidActivationToken);
        }

        invitation.UsedAtUtc = nowUtc;

        invitation.Raise(new UserInvitationAcceptedDomainEvent(invitation.UserId));

        context.UserCredentials.Add(new UserCredential
        {
            Id = Guid.NewGuid(),
            UserId = invitation.UserId,
            PasswordHash = passwordHasher.Hash(command.NewPassword),
            MustChangePassword = false,
            SecurityStamp = Guid.NewGuid()
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
