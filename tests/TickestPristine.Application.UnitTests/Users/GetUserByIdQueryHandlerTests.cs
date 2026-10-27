using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Users.GetById;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using TickestPristine.Application.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetUserByIdQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotSelfAndLacksReadPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = UserCodes.New()
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        var callerId = Guid.NewGuid();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Users.Read, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetUserByIdQueryHandler(context, userContext, permissionProvider);
        var query = new GetUserByIdQuery(user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetUserByIdQueryHandler(context, userContext, permissionProvider);
        var query = new GetUserByIdQuery(userId);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(query.UserId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUser_WhenCallerIsSelf()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = UserCodes.New()
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetUserByIdQueryHandler(context, userContext, permissionProvider);
        var query = new GetUserByIdQuery(user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("someone@example.com");
    }

    [Fact]
    public async Task Handle_Should_ReturnUser_WhenCallerHasReadPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = UserCodes.New()
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        var callerId = Guid.NewGuid();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Users.Read, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GetUserByIdQueryHandler(context, userContext, permissionProvider);
        var query = new GetUserByIdQuery(user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnSectorAndDepartment_WhenUserHasSector()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context, "TI", "Tecnologia");
        User user = CreateUser();
        user.SectorId = sector.Id;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        GetUserByIdQueryHandler handler = CreateSelfHandler(context, user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(new GetUserByIdQuery(user.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SectorId.ShouldBe(sector.Id);
        result.Value.SectorName.ShouldBe("TI");
        result.Value.DepartmentName.ShouldBe("Tecnologia");
        result.Value.SectorIsActive.ShouldBe(true);
    }

    [Fact]
    public async Task Handle_Should_ReturnNullSector_WhenUserHasNoSector()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedSectorAsync(context, "TI", "Tecnologia");
        User user = CreateUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        GetUserByIdQueryHandler handler = CreateSelfHandler(context, user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(new GetUserByIdQuery(user.Id), CancellationToken.None);

        // Assert
        result.Value.SectorId.ShouldBeNull();
        result.Value.SectorName.ShouldBeNull();
        result.Value.DepartmentName.ShouldBeNull();
        result.Value.SectorIsActive.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnInactiveSector_WhenSectorIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context, "TI", "Tecnologia", isActive: false);
        User user = CreateUser();
        user.SectorId = sector.Id;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        GetUserByIdQueryHandler handler = CreateSelfHandler(context, user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(new GetUserByIdQuery(user.Id), CancellationToken.None);

        // Assert
        result.Value.SectorId.ShouldBe(sector.Id);
        result.Value.SectorName.ShouldBe("TI");
        result.Value.DepartmentName.ShouldBe("Tecnologia");
        result.Value.SectorIsActive.ShouldBe(false);
    }

    /// <summary>
    /// Handler em que quem consulta é o próprio usuário, que sempre pode ler o próprio perfil.
    /// </summary>
    private static GetUserByIdQueryHandler CreateSelfHandler(TestDbContext context, Guid userId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);

        return new GetUserByIdQueryHandler(context, userContext, Substitute.For<IPermissionProvider>());
    }
}
