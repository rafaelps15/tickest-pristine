using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Users;
using TickestPristine.Application.Users.Login;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class LoginUserCommandHandlerTests : BaseHandlerTest
{
    private const string Email = "test@example.com";
    private const string Password = "Password123!";

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new LoginUserCommandHandler(
            context,
            Substitute.For<IPasswordHasher>(),
            Substitute.For<ITokenProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenPasswordIsInvalid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var handler = new LoginUserCommandHandler(
            context,
            passwordHasher,
            Substitute.For<ITokenProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_Should_ReturnTokensAndPersistRefreshToken_WhenCredentialsAreValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);


        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("access-token");
        DateTime expiresOnUtc = DateTime.UtcNow.AddDays(7);
        tokenProvider.GenerateRefreshToken().Returns(new GeneratedRefreshToken("refresh-token", expiresOnUtc));

        var handler = new LoginUserCommandHandler(
            context,
            passwordHasher,
            tokenProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.RefreshToken.ShouldBe("refresh-token");

        await tokenProvider.Received(1).CreateAsync(Arg.Is<User>(u => u.Id == user.Id), Arg.Any<CancellationToken>());

        RefreshToken refreshToken = await context.RefreshTokens.SingleAsync();
        refreshToken.TokenHash.ShouldBe(RefreshTokenHasher.Hash("refresh-token"));
        refreshToken.ExpiresOnUtc.ShouldBe(expiresOnUtc);
        refreshToken.DomainEvents.ShouldContain(domainEvent => domainEvent is RefreshTokenCreatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnDeactivated_WhenUserIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, isActive: false);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();

        var handler = new LoginUserCommandHandler(
            context,
            passwordHasher,
            tokenProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Deactivated);
        await tokenProvider.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = Email,
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}",
            IsActive = isActive
        };

        context.Users.Add(user);
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = user.Id, PasswordHash = "hash" });

        await context.SaveChangesAsync();

        return user;
    }
}
