using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.GetAll;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetUsersQueryHandlerTests : BaseHandlerTest
{
    private static GetUsersQuery AllUsers => new(UserStatusFilter.All, null, null, null, null, false, new PageRequest(1, 20));

    [Fact]
    public async Task Handle_Should_ReturnAllUsers_WhenNoFilterIsGiven()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User active = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        User inactive = AddUser(context, "Bruno", "bruno@tickestpristine.dev", isActive: false);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(AllUsers, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(2);
        result.Value.Items.ShouldContain(u => u.Id == active.Id && u.Email == active.Email && u.IsActive);
        result.Value.Items.ShouldContain(u => u.Id == inactive.Id && !u.IsActive && u.DeactivatedAtUtc == inactive.DeactivatedAtUtc);
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveUsers_WhenStatusIsActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User active = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        AddUser(context, "Bruno", "bruno@tickestpristine.dev", isActive: false);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { Status = UserStatusFilter.Active },
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(active.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyInactiveUsers_WhenStatusIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        User inactive = AddUser(context, "Bruno", "bruno@tickestpristine.dev", isActive: false);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { Status = UserStatusFilter.Inactive },
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(inactive.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnMatchingUsers_WhenSearchMatchesNameOrEmail()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User byName = AddUser(context, "Mariana", "mari@tickestpristine.dev", isActive: true);
        User byEmail = AddUser(context, "Carlos", "carlos.mariano@tickestpristine.dev", isActive: true);
        AddUser(context, "Pedro", "pedro@tickestpristine.dev", isActive: true);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { Search = "  arian " },
            CancellationToken.None);

        // Assert
        result.Value.Items.Select(u => u.Id).ShouldBe([byEmail.Id, byName.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_Should_ReturnUsersCreatedInPeriod_WhenPeriodIsGiven()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User before = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        User firstDay = AddUser(context, "Bruno", "bruno@tickestpristine.dev", isActive: true);
        User lastDay = AddUser(context, "Carla", "carla@tickestpristine.dev", isActive: true);
        User after = AddUser(context, "Davi", "davi@tickestpristine.dev", isActive: true);
        before.CreatedAtUtc = new DateTime(2026, 9, 30, 23, 59, 0, DateTimeKind.Utc);
        firstDay.CreatedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        lastDay.CreatedAtUtc = new DateTime(2026, 10, 2, 23, 59, 0, DateTimeKind.Utc);
        after.CreatedAtUtc = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { CreatedFrom = new DateOnly(2026, 10, 1), CreatedTo = new DateOnly(2026, 10, 2) },
            CancellationToken.None);

        // Assert
        result.Value.Items.Select(u => u.Id).ShouldBe([firstDay.Id, lastDay.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyPage_WhenCreatedFromIsAfterCreatedTo()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { CreatedFrom = new DateOnly(2026, 10, 2), CreatedTo = new DateOnly(2026, 10, 1) },
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnRequestedPage_WhenUsersExceedPageSize()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        AddUser(context, "Bruno", "bruno@tickestpristine.dev", isActive: true);
        User third = AddUser(context, "Carla", "carla@tickestpristine.dev", isActive: true);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { Paging = new PageRequest(2, 2) },
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(third.Id);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.HasPreviousPage.ShouldBeTrue();
        result.Value.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnAssignedRoles_WhenUserHasRoles()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        Role role = CreateRole("Atendente");
        context.Roles.Add(role);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = role.Name });
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(AllUsers, CancellationToken.None);

        // Assert
        UserSummaryResponse response = result.Value.Items.Single(u => u.Id == user.Id);
        response.Roles.ShouldContain(r => r.Name == role.Name && r.Name == "Atendente");
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyRoles_WhenUserHasNoRoles()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(AllUsers, CancellationToken.None);

        // Assert
        result.Value.Items.Single(u => u.Id == user.Id).Roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnSectorAndDepartment_WhenUserHasSector()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context, "TI", "Tecnologia");
        User user = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        user.SectorId = sector.Id;
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(AllUsers, CancellationToken.None);

        // Assert
        UserSummaryResponse response = result.Value.Items.Single(u => u.Id == user.Id);
        response.SectorId.ShouldBe(sector.Id);
        response.SectorName.ShouldBe("TI");
        response.DepartmentName.ShouldBe("Tecnologia");
        response.SectorIsActive.ShouldBe(true);
    }

    [Fact]
    public async Task Handle_Should_ReturnNullSector_WhenUserHasNoSector()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedSectorAsync(context, "TI", "Tecnologia");
        User user = AddUser(context, "Eva", "eva@tickestpristine.dev", isActive: true);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(AllUsers, CancellationToken.None);

        // Assert
        UserSummaryResponse response = result.Value.Items.Single(u => u.Id == user.Id);
        response.SectorId.ShouldBeNull();
        response.SectorName.ShouldBeNull();
        response.DepartmentName.ShouldBeNull();
        response.SectorIsActive.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnInactiveSector_WhenSectorIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context, "TI", "Tecnologia", isActive: false);
        User user = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        user.SectorId = sector.Id;
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(AllUsers, CancellationToken.None);

        // Assert
        UserSummaryResponse response = result.Value.Items.Single(u => u.Id == user.Id);
        response.SectorId.ShouldBe(sector.Id);
        response.SectorName.ShouldBe("TI");
        response.DepartmentName.ShouldBe("Tecnologia");
        response.SectorIsActive.ShouldBe(false);
    }

    [Fact]
    public async Task Handle_Should_ReturnPendingActivationWithInvitationExpiration_WhenUserHasNoCredential()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User pending = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        User activated = AddUser(context, "Beto", "beto@tickestpristine.dev", isActive: true);
        context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = activated.Id, PasswordHash = "hash" });
        await context.SaveChangesAsync();
        UserInvitation invitation = await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(
            context, pending.Id, "token-do-convite", DefaultUtcNow.AddHours(-1));

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(AllUsers, CancellationToken.None);

        // Assert
        UserSummaryResponse pendingResponse = result.Value.Items.Single(u => u.Id == pending.Id);
        pendingResponse.PendingActivation.ShouldBeTrue();
        pendingResponse.InvitationExpiresAtUtc.ShouldBe(invitation.ExpiresAtUtc);

        UserSummaryResponse activatedResponse = result.Value.Items.Single(u => u.Id == activated.Id);
        activatedResponse.PendingActivation.ShouldBeFalse();
        activatedResponse.InvitationExpiresAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnNullInvitationExpiration_WhenPendingUserInvitationIsExpiredOrRevoked()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User expired = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        User revoked = AddUser(context, "Beto", "beto@tickestpristine.dev", isActive: true);
        await context.SaveChangesAsync();
        await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(
            context, expired.Id, "token-vencido", DefaultUtcNow.AddHours(-73));
        await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(
            context, revoked.Id, "token-revogado", DefaultUtcNow.AddHours(-1), revoked: true);

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(AllUsers, CancellationToken.None);

        // Assert
        result.Value.Items.ShouldAllBe(u => u.PendingActivation && u.InvitationExpiresAtUtc == null);
    }

    private static User AddUser(TestDbContext context, string firstName, string email, bool isActive)
    {
        User user = CreateUser(isActive);
        user.FirstName = firstName;
        user.LastName = "Silva";
        user.Email = email;
        user.DeactivatedAtUtc = isActive ? null : new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

        context.Users.Add(user);

        return user;
    }
}
