using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.ActivateAccount;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class ActivateUserAccountCommandHandlerTests : BaseHandlerTest
{
    private const string Token = "token-do-convite";
    private const string NewPassword = "Password123!";

    [Fact]
    public async Task Handle_Should_CreateCredentialAndMarkInvitationUsed_WhenTokenIsValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        UserInvitation invitation = await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(
            context, user.Id, Token, DefaultUtcNow.AddHours(-1));
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(NewPassword).Returns("hashed-password");
        ActivateUserAccountCommandHandler handler = CreateHandler(context, passwordHasher);

        // Act
        Result result = await handler.Handle(new ActivateUserAccountCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        UserCredential credential = await context.UserCredentials.SingleAsync(c => c.UserId == user.Id);
        credential.PasswordHash.ShouldBe("hashed-password");
        credential.MustChangePassword.ShouldBeFalse();
        credential.SecurityStamp.ShouldNotBe(Guid.Empty);

        UserInvitation used = await context.UserInvitations.SingleAsync(i => i.Id == invitation.Id);
        used.UsedAtUtc.ShouldBe(DefaultUtcNow);
        used.DomainEvents.OfType<UserInvitationAcceptedDomainEvent>().ShouldHaveSingleItem().UserId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidActivationToken_WhenTokenDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ActivateUserAccountCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result result = await handler.Handle(new ActivateUserAccountCommand("inexistente", NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidActivationToken);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidActivationToken_WhenInvitationIsExpired()
    {
        // Arrange: emitido há 72 horas, então vence exatamente agora
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(context, user.Id, Token, DefaultUtcNow.AddHours(-72));
        ActivateUserAccountCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result result = await handler.Handle(new ActivateUserAccountCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidActivationToken);
        (await context.UserCredentials.AnyAsync(c => c.UserId == user.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidActivationToken_WhenInvitationIsRevoked()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(
            context, user.Id, Token, DefaultUtcNow.AddHours(-1), revoked: true);
        ActivateUserAccountCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result result = await handler.Handle(new ActivateUserAccountCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidActivationToken);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidActivationToken_WhenInvitationWasAlreadyUsed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        UserInvitation invitation = await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(
            context, user.Id, Token, DefaultUtcNow.AddHours(-1));
        invitation.UsedAtUtc = DefaultUtcNow.AddMinutes(-30);
        await context.SaveChangesAsync();
        ActivateUserAccountCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result result = await handler.Handle(new ActivateUserAccountCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidActivationToken);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidActivationToken_WhenUserIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context, isActive: false);
        await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(context, user.Id, Token, DefaultUtcNow.AddHours(-1));
        ActivateUserAccountCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result result = await handler.Handle(new ActivateUserAccountCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidActivationToken);
        (await context.UserCredentials.AnyAsync(c => c.UserId == user.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidActivationToken_WhenUserAlreadyHasCredential()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(context, user.Id, Token, DefaultUtcNow.AddHours(-1));
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = user.Id, PasswordHash = "existing" });
        await context.SaveChangesAsync();
        ActivateUserAccountCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result result = await handler.Handle(new ActivateUserAccountCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidActivationToken);
        (await context.UserCredentials.SingleAsync(c => c.UserId == user.Id)).PasswordHash.ShouldBe("existing");
    }

    private static ActivateUserAccountCommandHandler CreateHandler(TestDbContext context, IPasswordHasher passwordHasher) =>
        new(context, passwordHasher, CreateClock());

    private static async Task<User> SeedUserAsync(TestDbContext context, bool isActive = true)
    {
        User user = CreateUser(isActive);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }
}
