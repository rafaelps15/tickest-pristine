using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.ChangePassword;

internal sealed class ChangeUserPasswordCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPasswordHasher passwordHasher)
    : ICommandHandler<ChangeUserPasswordCommand>
{
    public async Task<Result> Handle(ChangeUserPasswordCommand command, CancellationToken cancellationToken)
    {
        UserCredential? credential = await context.UserCredentials
            .SingleOrDefaultAsync(c => c.UserId == userContext.UserId, cancellationToken);

        if (credential is null)
        {
            return Result.Failure(UserErrors.NotFound(userContext.UserId));
        }

        if (!passwordHasher.Verify(command.CurrentPassword, credential.PasswordHash))
        {
            return Result.Failure(UserErrors.InvalidCurrentPassword);
        }

        credential.PasswordHash = passwordHasher.Hash(command.NewPassword);
        credential.MustChangePassword = false;

        credential.Raise(new UserPasswordChangedDomainEvent(credential.UserId));

        // Revoga todas as sessões ativas do usuário.
        await UserSessions.RevokeAllAsync(context, credential.UserId, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
