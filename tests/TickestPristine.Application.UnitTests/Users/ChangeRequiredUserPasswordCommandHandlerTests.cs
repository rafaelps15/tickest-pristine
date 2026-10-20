using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Users;
using TickestPristine.Application.Users.ChangeRequiredPassword;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class ChangeRequiredUserPasswordCommandHandlerTests : BaseHandlerTest
{
    private const string Email = "test@example.com";
    private const string CurrentPassword = "Current123!";
    private const string NewPassword = "NewPassword123!";

    private static readonly ChangeRequiredUserPasswordCommand Command = new(Email, CurrentPassword, NewPassword);

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new ChangeRequiredUserPasswordCommandHandler(
            context,
            Substitute.For<IPasswordHasher>(),
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenCurrentPasswordIsWrong()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var handler = new ChangeRequiredUserPasswordCommandHandler(
            context,
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);

        UserCredential credential = await context.UserCredentials.SingleAsync();
        credential.PasswordHash.ShouldBe("old-hash");
        credential.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnDeactivated_WhenUserIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, isActive: false);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(CurrentPassword, "old-hash").Returns(true);

        var handler = new ChangeRequiredUserPasswordCommandHandler(
            context,
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Deactivated);
    }

    [Fact]
    public async Task Handle_Should_ReturnPasswordChangeNotRequired_WhenPasswordIsNotProvisional()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, mustChangePassword: false);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(CurrentPassword, "old-hash").Returns(true);

        var handler = new ChangeRequiredUserPasswordCommandHandler(
            context,
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.PasswordChangeNotRequired);

        UserCredential credential = await context.UserCredentials.SingleAsync();
        credential.PasswordHash.ShouldBe("old-hash");
    }

    [Fact]
    public async Task Handle_Should_ChangePasswordAndReturnTokens_WhenPasswordIsProvisional()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(CurrentPassword, "old-hash").Returns(true);
        passwordHasher.Hash(NewPassword).Returns("new-hash");

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("access-token");
        tokenProvider.GenerateRefreshToken().Returns(new GeneratedRefreshToken("refresh-token", DateTime.UtcNow.AddDays(7)));

        var handler = new ChangeRequiredUserPasswordCommandHandler(
            context,
            passwordHasher,
            tokenProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.RefreshToken.ShouldBe("refresh-token");

        UserCredential credential = await context.UserCredentials.SingleAsync();
        credential.PasswordHash.ShouldBe("new-hash");
        credential.MustChangePassword.ShouldBeFalse();
        credential.SecurityStamp.ShouldNotBe(Guid.Empty);
        credential.DomainEvents.ShouldContain(domainEvent => domainEvent is UserPasswordChangedDomainEvent);

        RefreshToken refreshToken = await context.RefreshTokens.SingleAsync();
        refreshToken.UserId.ShouldBe(user.Id);
        refreshToken.TokenHash.ShouldBe(RefreshTokenHasher.Hash("refresh-token"));
        refreshToken.SecurityStamp.ShouldBe(credential.SecurityStamp);
    }

    [Fact]
    public async Task Handle_Should_RevokeOnlyCallerPreviousRefreshTokens_WhenPasswordIsChanged()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        var otherUserId = Guid.NewGuid();
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), TokenHash = "old-hash-1", UserId = user.Id, ExpiresOnUtc = DateTime.UtcNow.AddDays(7) });
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), TokenHash = "other-hash", UserId = otherUserId, ExpiresOnUtc = DateTime.UtcNow.AddDays(7) });
        await context.SaveChangesAsync();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(CurrentPassword, "old-hash").Returns(true);
        passwordHasher.Hash(NewPassword).Returns("new-hash");

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("access-token");
        tokenProvider.GenerateRefreshToken().Returns(new GeneratedRefreshToken("refresh-token", DateTime.UtcNow.AddDays(7)));

        var handler = new ChangeRequiredUserPasswordCommandHandler(
            context,
            passwordHasher,
            tokenProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<string> remainingHashes = await context.RefreshTokens.Select(r => r.TokenHash).ToListAsync();
        remainingHashes.ShouldBe([RefreshTokenHasher.Hash("refresh-token"), "other-hash"], ignoreOrder: true);
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool isActive = true, bool mustChangePassword = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = Email,
            FirstName = "Test",
            LastName = "User",
            Code = UserCodes.New(),
            IsActive = isActive
        };

        context.Users.Add(user);
        context.UserCredentials.Add(new UserCredential
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PasswordHash = "old-hash",
            MustChangePassword = mustChangePassword
        });

        await context.SaveChangesAsync();

        return user;
    }
}
