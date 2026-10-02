using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Users;
using TickestPristine.Application.Users.Refresh;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class RefreshTokenCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenRefreshTokenDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new RefreshTokenCommandHandler(
            context,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("missing-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenRefreshTokenIsExpired()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        await SeedRefreshTokenAsync(context, "expired-token", expiresOnUtc: now.AddDays(-1));

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(
            context,
            Substitute.For<ITokenProvider>(),
            dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("expired-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_RotateTokenAndReturnNewTokens_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        User user = await SeedRefreshTokenAsync(context, "old-token", expiresOnUtc: now.AddDays(1));


        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("new-access-token");
        DateTime expiresOnUtc = now.AddDays(7);
        tokenProvider.GenerateRefreshToken().Returns(new GeneratedRefreshToken("new-refresh-token", expiresOnUtc));

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(
            context,
            tokenProvider,
            dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("old-token"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("new-access-token");
        result.Value.RefreshToken.ShouldBe("new-refresh-token");

        await tokenProvider.Received(1).CreateAsync(Arg.Is<User>(u => u.Id == user.Id), Arg.Any<CancellationToken>());

        RefreshToken stored = await context.RefreshTokens.SingleAsync();
        stored.TokenHash.ShouldBe(RefreshTokenHasher.Hash("new-refresh-token"));
        stored.ExpiresOnUtc.ShouldBe(expiresOnUtc);
        stored.DomainEvents.ShouldContain(domainEvent => domainEvent is RefreshTokenRotatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnDeactivated_WhenUserIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        await SeedRefreshTokenAsync(context, "valid-token", expiresOnUtc: now.AddDays(1), isActive: false);

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(
            context,
            Substitute.For<ITokenProvider>(),
            dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("valid-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Deactivated);
    }

    private static async Task<User> SeedRefreshTokenAsync(TestDbContext context, string token, DateTime expiresOnUtc, bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}",
            IsActive = isActive
        };

        context.Users.Add(user);
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), TokenHash = RefreshTokenHasher.Hash(token), UserId = user.Id, ExpiresOnUtc = expiresOnUtc });

        await context.SaveChangesAsync();

        return user;
    }
}
