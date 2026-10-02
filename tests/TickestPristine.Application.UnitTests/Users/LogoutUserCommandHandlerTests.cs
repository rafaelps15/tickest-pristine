using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users;
using TickestPristine.Application.Users.Logout;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class LogoutUserCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_RemoveRefreshTokenAndRaiseDomainEvent_WhenTokenBelongsToCaller()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        RefreshToken refreshToken = await SeedRefreshTokenAsync(context, UserId, "my-token");

        var handler = new LogoutUserCommandHandler(context, CreateUserContext(UserId));

        // Act
        Result result = await handler.Handle(new LogoutUserCommand("my-token"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.RefreshTokens.AnyAsync(rt => rt.Id == refreshToken.Id)).ShouldBeFalse();
        refreshToken.DomainEvents.ShouldContain(domainEvent => domainEvent is RefreshTokenRevokedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_KeepRefreshToken_WhenTokenBelongsToAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        RefreshToken refreshToken = await SeedRefreshTokenAsync(context, Guid.NewGuid(), "someone-elses-token");

        var handler = new LogoutUserCommandHandler(context, CreateUserContext(UserId));

        // Act
        Result result = await handler.Handle(new LogoutUserCommand("someone-elses-token"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.RefreshTokens.AnyAsync(rt => rt.Id == refreshToken.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenTokenDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new LogoutUserCommandHandler(context, CreateUserContext(UserId));

        // Act
        Result result = await handler.Handle(new LogoutUserCommand("missing-token"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    private static IUserContext CreateUserContext(Guid userId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);

        return userContext;
    }

    private static async Task<RefreshToken> SeedRefreshTokenAsync(TestDbContext context, Guid userId, string token)
    {
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = RefreshTokenHasher.Hash(token),
            UserId = userId,
            ExpiresOnUtc = DateTime.UtcNow.AddDays(1)
        };

        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        return refreshToken;
    }
}
