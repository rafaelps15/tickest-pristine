using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Users;
using TickestPristine.Application.Users.Login;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

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
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenCredentialDoesNotExist()
    {
        // Arrange: o usuário existe, mas não tem linha de credencial
        await using TestDbContext context = CreateDbContext();
        User user = CreateUser();
        user.Email = Email;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();

        var handler = new LoginUserCommandHandler(
            context,
            passwordHasher,
            tokenProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await tokenProvider.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ResetFailedAttempts_WhenPasswordIsCorrectButPasswordChangeIsRequired()
    {
        // Arrange: quatro senhas erradas seguidas e a senha ainda provisória
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, mustChangePassword: true);
        UserCredential credential = await context.UserCredentials.SingleAsync();
        credential.FailedLoginAttempts = 4;
        await context.SaveChangesAsync();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc));

        var handler = new LoginUserCommandHandler(context, passwordHasher, Substitute.For<ITokenProvider>(), dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert: o login é recusado, mas o zeramento do contador já foi gravado no banco
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.PasswordChangeRequired);

        context.ChangeTracker.Clear();
        UserCredential stored = await context.UserCredentials.AsNoTracking().SingleAsync();
        stored.FailedLoginAttempts.ShouldBe(0);
        stored.LockoutEndsAtUtc.ShouldBeNull();
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
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        (await context.UserCredentials.SingleAsync()).FailedLoginAttempts.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_LockAccount_WhenMaxFailedAttemptsIsReached()
    {
        // Arrange: a conta já tem uma tentativa a menos que o limite
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        UserCredential credential = await context.UserCredentials.SingleAsync();
        credential.FailedLoginAttempts = UserCredentialsVerifier.MaxFailedAttempts - 1;
        await context.SaveChangesAsync();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var nowUtc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);

        var handler = new LoginUserCommandHandler(context, passwordHasher, Substitute.For<ITokenProvider>(), dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.LockedOut);
        UserCredential locked = await context.UserCredentials.SingleAsync();
        locked.LockoutEndsAtUtc.ShouldBe(nowUtc.Add(UserCredentialsVerifier.LockoutDuration));
        locked.FailedLoginAttempts.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnLockedOut_WhenAccountIsLockedEvenWithCorrectPassword()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        var nowUtc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        UserCredential credential = await context.UserCredentials.SingleAsync();
        credential.LockoutEndsAtUtc = nowUtc.AddMinutes(5);
        await context.SaveChangesAsync();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);

        var handler = new LoginUserCommandHandler(context, passwordHasher, tokenProvider, dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.LockedOut);
        passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await tokenProvider.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
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
        DateTime nowUtc = DateTime.UtcNow;
        DateTime expiresOnUtc = nowUtc.AddDays(7);
        tokenProvider.GenerateRefreshToken().Returns(new GeneratedRefreshToken("refresh-token", expiresOnUtc));
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);

        var handler = new LoginUserCommandHandler(
            context,
            passwordHasher,
            tokenProvider,
            dateTimeProvider);

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
        refreshToken.SessionExpiresOnUtc.ShouldBe(nowUtc.AddDays(30));
        refreshToken.SecurityStamp.ShouldBe((await context.UserCredentials.SingleAsync()).SecurityStamp);
        refreshToken.DomainEvents.ShouldContain(domainEvent => domainEvent is RefreshTokenCreatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_RemoveExpiredRefreshTokensOfUser_WhenCredentialsAreValid()
    {
        // Arrange: uma sessão expirada e outra ainda válida do mesmo usuário
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);

        var nowUtc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = "expired",
            UserId = user.Id,
            ExpiresOnUtc = nowUtc.AddDays(-1)
        };
        var validToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = "valid",
            UserId = user.Id,
            ExpiresOnUtc = nowUtc.AddDays(1)
        };
        context.RefreshTokens.AddRange(expiredToken, validToken);
        await context.SaveChangesAsync();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("access-token");
        tokenProvider.GenerateRefreshToken().Returns(new GeneratedRefreshToken("refresh-token", nowUtc.AddDays(7)));
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);

        var handler = new LoginUserCommandHandler(context, passwordHasher, tokenProvider, dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<Guid> remainingTokenIds = await context.RefreshTokens.Select(r => r.Id).ToListAsync();
        remainingTokenIds.ShouldNotContain(expiredToken.Id);
        remainingTokenIds.ShouldContain(validToken.Id);
        remainingTokenIds.Count.ShouldBe(2);
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
            tokenProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Deactivated);
        await tokenProvider.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnPasswordChangeRequired_WhenPasswordIsProvisional()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, mustChangePassword: true);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();

        var handler = new LoginUserCommandHandler(
            context,
            passwordHasher,
            tokenProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.PasswordChangeRequired);
        await tokenProvider.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        (await context.RefreshTokens.AnyAsync()).ShouldBeFalse();
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool isActive = true, bool mustChangePassword = false)
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
            PasswordHash = "hash",
            MustChangePassword = mustChangePassword
        });

        await context.SaveChangesAsync();

        return user;
    }
}
