using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Notifications;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users;
using TickestPristine.Application.Users.ResendInvitation;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class ResendUserInvitationCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid CallerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ResendUserInvitationCommandHandler handler = CreateHandler(context, Substitute.For<IEmailSender>());
        var command = new ResendUserInvitationCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.NotFound(command.UserId));
    }

    [Fact]
    public async Task Handle_Should_ReturnInvitationUserDeactivated_WhenUserIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context, isActive: false);
        ResendUserInvitationCommandHandler handler = CreateHandler(context, Substitute.For<IEmailSender>());

        // Act
        Result result = await handler.Handle(new ResendUserInvitationCommand(user.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvitationUserDeactivated);
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyActivated_WhenUserHasCredential()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = user.Id, PasswordHash = "hash" });
        await context.SaveChangesAsync();
        ResendUserInvitationCommandHandler handler = CreateHandler(context, Substitute.For<IEmailSender>());

        // Act
        Result result = await handler.Handle(new ResendUserInvitationCommand(user.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.AlreadyActivated);
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotManagePrivilegedUser_WhenUserHasAdministrativePermissionCallerLacks()
    {
        // Arrange: quem chama só cadastra; o alvo também gerencia funções
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        await GrantPermissionsAsync(context, user.Id, PermissionCodes.Roles.Manage);

        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(CallerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.Create]);
        IEmailSender emailSender = Substitute.For<IEmailSender>();
        ResendUserInvitationCommandHandler handler = CreateHandler(context, emailSender, permissionProvider);

        // Act
        Result result = await handler.Handle(new ResendUserInvitationCommand(user.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.CannotManagePrivilegedUser([PermissionCatalog.GetDisplayName(PermissionCodes.Roles.Manage)]));
        await emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnInvitationLimitReached_WhenFiveInvitationsWereIssuedInTheLast24Hours()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        for (int i = 1; i <= 5; i++)
        {
            await SeedInvitationAsync(context, user.Id, $"token-{i}", createdAtUtc: DefaultUtcNow.AddHours(-i), revoked: i > 1);
        }

        IEmailSender emailSender = Substitute.For<IEmailSender>();
        ResendUserInvitationCommandHandler handler = CreateHandler(context, emailSender);

        // Act
        Result result = await handler.Handle(new ResendUserInvitationCommand(user.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvitationLimitReached(5, 24));
        (await context.UserInvitations.CountAsync(i => i.UserId == user.Id)).ShouldBe(5);
        await emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_IgnoreInvitationsOlderThan24Hours_WhenCountingTheLimit()
    {
        // Arrange: cinco convites, todos com mais de 24 horas
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        for (int i = 1; i <= 5; i++)
        {
            await SeedInvitationAsync(context, user.Id, $"token-{i}", createdAtUtc: DefaultUtcNow.AddHours(-24 - i), revoked: true);
        }

        ResendUserInvitationCommandHandler handler = CreateHandler(context, Substitute.For<IEmailSender>());

        // Act
        Result result = await handler.Handle(new ResendUserInvitationCommand(user.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_RevokePreviousInvitationIssueNewOneAndSendEmail_WhenUserIsPending()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        UserInvitation previous = await SeedInvitationAsync(context, user.Id, "token-antigo", DefaultUtcNow.AddHours(-1));
        IEmailSender emailSender = Substitute.For<IEmailSender>();
        ResendUserInvitationCommandHandler handler = CreateHandler(context, emailSender);

        // Act
        Result result = await handler.Handle(new ResendUserInvitationCommand(user.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserInvitations.SingleAsync(i => i.Id == previous.Id)).RevokedAtUtc.ShouldBe(DefaultUtcNow);

        UserInvitation current = await context.UserInvitations.SingleAsync(i => i.UserId == user.Id && i.RevokedAtUtc == null);
        current.ExpiresAtUtc.ShouldBe(DefaultUtcNow.AddHours(72));

        var message = (EmailMessage)emailSender.ReceivedCalls().ShouldHaveSingleItem().GetArguments()[0]!;
        message.To.ShouldBe(user.Email);
        UserInvitations.HashToken(RegisterUserCommandHandlerTests.ExtractToken(message.TextBody)).ShouldBe(current.TokenHash);
    }

    private static ResendUserInvitationCommandHandler CreateHandler(
        TestDbContext context,
        IEmailSender emailSender,
        IPermissionProvider? permissionProvider = null)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CallerId);

        return new ResendUserInvitationCommandHandler(
            context,
            userContext,
            permissionProvider ?? Substitute.For<IPermissionProvider>(),
            emailSender,
            RegisterUserCommandHandlerTests.CreateLinkBuilder(),
            CreateClock(),
            new RecordingLogger<ResendUserInvitationCommandHandler>());
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool isActive = true)
    {
        User user = CreateUser(isActive);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    internal static async Task<UserInvitation> SeedInvitationAsync(
        TestDbContext context,
        Guid userId,
        string token,
        DateTime createdAtUtc,
        bool revoked = false)
    {
        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = UserInvitations.HashToken(token),
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddHours(72),
            RevokedAtUtc = revoked ? createdAtUtc.AddMinutes(1) : null
        };

        context.UserInvitations.Add(invitation);
        await context.SaveChangesAsync();

        return invitation;
    }
}
