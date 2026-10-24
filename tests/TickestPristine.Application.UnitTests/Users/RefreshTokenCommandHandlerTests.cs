using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Users;
using TickestPristine.Application.Users.Refresh;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class RefreshTokenCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid CredentialSecurityStamp = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnInvalidRefreshToken_WhenRefreshTokenDoesNotExist()
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
    public async Task Handle_Should_ReturnInvalidRefreshToken_WhenRefreshTokenIsExpired()
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

    [Fact]
    public async Task Handle_Should_RemoveTokenAndReturnFailure_WhenPasswordChangedAfterSessionStarted()
    {
        // Arrange: a sessão guardou o carimbo de antes da troca de senha
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        await SeedRefreshTokenAsync(context, "stale-token", expiresOnUtc: now.AddDays(1), tokenSecurityStamp: Guid.NewGuid());

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();

        var handler = new RefreshTokenCommandHandler(context, tokenProvider, dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("stale-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
        (await context.RefreshTokens.AnyAsync()).ShouldBeFalse();
        await tokenProvider.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_KeepExpirationWithinSessionLimit_WhenRotating()
    {
        // Arrange: a sessão acaba antes da validade que um token novo teria
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        await SeedRefreshTokenAsync(context, "old-token", expiresOnUtc: now.AddDays(1));
        RefreshToken seeded = await context.RefreshTokens.SingleAsync();
        seeded.SessionExpiresOnUtc = now.AddDays(2);
        await context.SaveChangesAsync();

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("new-access-token");
        tokenProvider.GenerateRefreshToken().Returns(new GeneratedRefreshToken("new-refresh-token", now.AddDays(7)));
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(context, tokenProvider, dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("old-token"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        RefreshToken stored = await context.RefreshTokens.SingleAsync();
        stored.ExpiresOnUtc.ShouldBe(now.AddDays(2));
        stored.PreviousTokenHash.ShouldBe(RefreshTokenHasher.Hash("old-token"));
        stored.RotatedAtUtc.ShouldBe(now);
    }

    [Fact]
    public async Task Handle_Should_RevokeAllSessions_WhenRotatedTokenIsReusedAfterGracePeriod()
    {
        // Arrange: o token antigo volta um minuto depois da renovação
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        User user = await SeedRotatedTokenAsync(context, "old-token", rotatedAtUtc: now.AddMinutes(-1));

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(context, Substitute.For<ITokenProvider>(), dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("old-token"),
            CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
        (await context.RefreshTokens.AnyAsync(r => r.UserId == user.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_KeepSessions_WhenRotatedTokenIsReusedWithinGracePeriod()
    {
        // Arrange: duas abas renovando juntas, a segunda chega logo depois da primeira
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        User user = await SeedRotatedTokenAsync(context, "old-token", rotatedAtUtc: now.AddSeconds(-5));

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(context, Substitute.For<ITokenProvider>(), dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("old-token"),
            CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
        (await context.RefreshTokens.AnyAsync(r => r.UserId == user.Id)).ShouldBeTrue();
    }

    private static async Task<User> SeedRotatedTokenAsync(TestDbContext context, string previousToken, DateTime rotatedAtUtc)
    {
        User user = await SeedRefreshTokenAsync(context, "current-token", expiresOnUtc: rotatedAtUtc.AddDays(7));

        RefreshToken current = await context.RefreshTokens.SingleAsync(r => r.UserId == user.Id);
        current.PreviousTokenHash = RefreshTokenHasher.Hash(previousToken);
        current.RotatedAtUtc = rotatedAtUtc;
        await context.SaveChangesAsync();

        return user;
    }

    private static async Task<User> SeedRefreshTokenAsync(
        TestDbContext context,
        string token,
        DateTime expiresOnUtc,
        bool isActive = true,
        Guid? tokenSecurityStamp = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
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
            PasswordHash = "hash",
            SecurityStamp = CredentialSecurityStamp
        });
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = RefreshTokenHasher.Hash(token),
            UserId = user.Id,
            ExpiresOnUtc = expiresOnUtc,
            SessionExpiresOnUtc = expiresOnUtc.AddDays(30),
            SecurityStamp = tokenSecurityStamp ?? CredentialSecurityStamp
        });

        await context.SaveChangesAsync();

        return user;
    }
}
