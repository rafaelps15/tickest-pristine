using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Logout;

internal sealed class LogoutUserCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext)
    : ICommandHandler<LogoutUserCommand>
{
    public async Task<Result> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        string tokenHash = RefreshTokenHasher.Hash(command.RefreshToken);

        RefreshToken? refreshToken = await context.RefreshTokens
            .SingleOrDefaultAsync(
                rt => rt.TokenHash == tokenHash && rt.UserId == userContext.UserId,
                cancellationToken);

        // Sair é idempotente: token já revogado, expirado ou de outra pessoa não gera erro nem revela se existe.
        if (refreshToken is null)
        {
            return Result.Success();
        }

        refreshToken.Raise(new RefreshTokenRevokedDomainEvent(refreshToken.Id, refreshToken.UserId));

        context.RefreshTokens.Remove(refreshToken);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
