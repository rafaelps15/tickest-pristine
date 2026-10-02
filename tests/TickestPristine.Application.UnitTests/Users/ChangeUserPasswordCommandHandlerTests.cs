using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Users.ChangePassword;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class ChangeUserPasswordCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCredentialDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();

        var handler = new ChangeUserPasswordCommandHandler(context, userContext, passwordHasher);
        var command = new ChangeUserPasswordCommand("Current123!", "NewPassword123!");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(userContext.UserId));
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCurrentPassword_WhenCurrentPasswordIsWrong()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = userId, PasswordHash = "old-hash" });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify("Wrong123!", "old-hash").Returns(false);

        var handler = new ChangeUserPasswordCommandHandler(context, userContext, passwordHasher);
        var command = new ChangeUserPasswordCommand("Wrong123!", "NewPassword123!");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCurrentPassword);

        UserCredential credential = await context.UserCredentials.SingleAsync(c => c.UserId == userId);
        credential.PasswordHash.ShouldBe("old-hash");
    }

    [Fact]
    public async Task Handle_Should_ChangePasswordAndRaiseDomainEvent_WhenCurrentPasswordIsCorrect()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = userId, PasswordHash = "old-hash" });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify("Current123!", "old-hash").Returns(true);
        passwordHasher.Hash("NewPassword123!").Returns("new-hash");

        var handler = new ChangeUserPasswordCommandHandler(context, userContext, passwordHasher);
        var command = new ChangeUserPasswordCommand("Current123!", "NewPassword123!");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        UserCredential updated = await context.UserCredentials.SingleAsync(c => c.UserId == userId);
        updated.PasswordHash.ShouldBe("new-hash");
        updated.DomainEvents.ShouldContain(domainEvent => domainEvent is UserPasswordChangedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_RevokeOnlyCallerRefreshTokens_WhenPasswordIsChanged()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = userId, PasswordHash = "old-hash" });
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), Token = "token-1", UserId = userId, ExpiresOnUtc = DateTime.UtcNow.AddDays(7) });
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), Token = "token-2", UserId = userId, ExpiresOnUtc = DateTime.UtcNow.AddDays(7) });
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), Token = "other-token", UserId = otherUserId, ExpiresOnUtc = DateTime.UtcNow.AddDays(7) });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify("Current123!", "old-hash").Returns(true);
        passwordHasher.Hash("NewPassword123!").Returns("new-hash");

        var handler = new ChangeUserPasswordCommandHandler(context, userContext, passwordHasher);
        var command = new ChangeUserPasswordCommand("Current123!", "NewPassword123!");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<RefreshToken> remaining = await context.RefreshTokens.ToListAsync();
        remaining.ShouldHaveSingleItem().UserId.ShouldBe(otherUserId);
    }
}
