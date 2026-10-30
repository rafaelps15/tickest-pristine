using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.ChangeSector;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class ChangeUserSectorCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid CallerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context);
        ChangeUserSectorCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());
        var command = new ChangeUserSectorCommand(Guid.NewGuid(), sector.Id);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.UserId));
    }

    [Fact]
    public async Task Handle_Should_SetSectorAndRaiseDomainEvent_WhenSectorIsActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        Sector sector = await SeedSectorAsync(context);
        ChangeUserSectorCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());

        // Act
        Result result = await handler.Handle(new ChangeUserSectorCommand(user.Id, sector.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        User stored = await context.Users.SingleAsync(u => u.Id == user.Id);
        stored.SectorId.ShouldBe(sector.Id);
        UserSectorChangedDomainEvent changed = stored.DomainEvents.OfType<UserSectorChangedDomainEvent>().ShouldHaveSingleItem();
        changed.UserId.ShouldBe(user.Id);
        changed.OldSectorId.ShouldBeNull();
        changed.NewSectorId.ShouldBe(sector.Id);
    }

    [Fact]
    public async Task Handle_Should_RemoveSector_WhenSectorIdIsNull()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context);
        User user = await SeedUserAsync(context, sector.Id);
        ChangeUserSectorCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());

        // Act
        Result result = await handler.Handle(new ChangeUserSectorCommand(user.Id, null), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        User stored = await context.Users.SingleAsync(u => u.Id == user.Id);
        stored.SectorId.ShouldBeNull();
        stored.DomainEvents.OfType<UserSectorChangedDomainEvent>().ShouldHaveSingleItem().OldSectorId.ShouldBe(sector.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnSectorNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context);
        User user = await SeedUserAsync(context, sector.Id);
        ChangeUserSectorCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());
        var missingSectorId = Guid.NewGuid();

        // Act
        Result result = await handler.Handle(new ChangeUserSectorCommand(user.Id, missingSectorId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NotFound(missingSectorId));
        (await context.Users.SingleAsync(u => u.Id == user.Id)).SectorId.ShouldBe(sector.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnSectorNotFoundAndKeepSector_WhenSectorIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector currentSector = await SeedSectorAsync(context);
        Sector deactivatedSector = await SeedSectorAsync(context, isActive: false);
        User user = await SeedUserAsync(context, currentSector.Id);
        ChangeUserSectorCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());

        // Act
        Result result = await handler.Handle(
            new ChangeUserSectorCommand(user.Id, deactivatedSector.Id),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NotFound(deactivatedSector.Id));
        (await context.Users.SingleAsync(u => u.Id == user.Id)).SectorId.ShouldBe(currentSector.Id);
    }

    [Fact]
    public async Task Handle_Should_ChangeNothing_WhenSectorIsAlreadyTheCurrentOne()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context);
        User user = await SeedUserAsync(context, sector.Id);
        ChangeUserSectorCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());

        // Act
        Result result = await handler.Handle(new ChangeUserSectorCommand(user.Id, sector.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == user.Id)).DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotManagePrivilegedUser_WhenUserHasAdministrativePermissionCallerLacks()
    {
        // Arrange: quem chama só edita usuários; o alvo também gerencia funções
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        await GrantPermissionsAsync(context, user.Id, PermissionCodes.Roles.Manage);
        Sector sector = await SeedSectorAsync(context);

        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(CallerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.Update]);
        ChangeUserSectorCommandHandler handler = CreateHandler(context, permissionProvider);

        // Act
        Result result = await handler.Handle(new ChangeUserSectorCommand(user.Id, sector.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.CannotManagePrivilegedUser([PermissionCatalog.GetDisplayName(PermissionCodes.Roles.Manage)]));
        (await context.Users.SingleAsync(u => u.Id == user.Id)).SectorId.ShouldBeNull();
    }

    private static ChangeUserSectorCommandHandler CreateHandler(TestDbContext context, IPermissionProvider permissionProvider)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CallerId);

        return new ChangeUserSectorCommandHandler(context, userContext, permissionProvider);
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, Guid? sectorId = null)
    {
        User user = CreateUser();
        user.SectorId = sectorId;

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private static async Task<Sector> SeedSectorAsync(TestDbContext context, bool isActive = true)
    {
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), $"Sector-{Guid.NewGuid():N}", description: null);
        if (!isActive)
        {
            sector.Deactivate();
        }

        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return sector;
    }
}
