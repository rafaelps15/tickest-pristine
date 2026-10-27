using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Notifications;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Register;

/// <summary>
/// Cadastra o usuário ativo, com a role Colaborador e sem credencial: a conta fica pendente de ativação até ele definir a
/// senha pelo convite enviado por e-mail. Ninguém além do titular conhece a senha.
/// </summary>
internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext context,
    IEmailSender emailSender,
    IActivationLinkBuilder activationLinkBuilder,
    IDateTimeProvider dateTimeProvider,
    ILogger<RegisterUserCommandHandler> logger)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
            Code = UserCodes.New(),
            IsActive = true,
            CreatedAtUtc = nowUtc
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        context.Users.Add(user);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = RoleNames.Collaborator });

        IssuedInvitation invitation = await UserInvitations.IssueAsync(context, user.Id, nowUtc, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        // Depois do commit: o e-mail só sai para um cadastro gravado, e a falha no envio não desfaz o cadastro.
        await UserInvitations.SendAsync(emailSender, activationLinkBuilder, logger, user, invitation, cancellationToken);

        return user.Id;
    }
}
