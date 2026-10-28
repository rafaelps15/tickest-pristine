using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.GetAll;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using SortDirection = TickestPristine.Application.Abstractions.Pagination.SortDirection;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetUsersQueryHandlerTests : BaseHandlerTest
{
    private static GetUsersQuery AllUsers => new(UserStatusFilter.All, null, null, null, null, false, null, null, new PageRequest(1, 20));

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

    [Theory]
    [InlineData("Atendente")]
    [InlineData("  Atendente ")]
    public async Task Handle_Should_ReturnOnlyUsersWithRole_WhenRoleNameIsGiven(string roleName)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User ana, User bruno, _, _) = await SeedUsersWithRolesAsync(context);

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { RoleName = roleName },
            CancellationToken.None);

        // Assert
        result.Value.Items.Select(u => u.Id).ShouldBe([ana.Id, bruno.Id], ignoreOrder: true);
        result.Value.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyPage_WhenNoUserHasRoleName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUsersWithRolesAsync(context);

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { RoleName = "Inexistente" },
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_IgnoreRoleName_WhenRoleNameIsBlank()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUsersWithRolesAsync(context);

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { RoleName = "  " },
            CancellationToken.None);

        // Assert
        result.Value.TotalCount.ShouldBe(4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Atendente")]
    public async Task Handle_Should_ReturnOnlyUsersWithoutRole_WhenWithoutRoleIsTrue(string? roleName)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (_, _, _, User davi) = await SeedUsersWithRolesAsync(context);

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { WithoutRole = true, RoleName = roleName },
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(davi.Id);
        result.Value.TotalCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(null, null, new[] { "Ana", "Bruno", "Carla", "Davi" })]
    [InlineData(UserSortField.Name, null, new[] { "Ana", "Bruno", "Carla", "Davi" })]
    [InlineData(UserSortField.Name, SortDirection.Descending, new[] { "Davi", "Carla", "Bruno", "Ana" })]
    [InlineData(null, SortDirection.Descending, new[] { "Davi", "Carla", "Bruno", "Ana" })]
    [InlineData(UserSortField.CreatedAt, null, new[] { "Davi", "Carla", "Bruno", "Ana" })]
    [InlineData(UserSortField.CreatedAt, SortDirection.Ascending, new[] { "Ana", "Bruno", "Carla", "Davi" })]
    public async Task Handle_Should_SortUsers_WhenSortIsGiven(
        UserSortField? sortBy,
        SortDirection? sortDirection,
        string[] expectedFirstNames)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        // Cadastro na ordem inversa do nome, para a ordem por data não coincidir com a ordem por nome por acaso.
        await SeedUsersWithRolesAsync(context);
        foreach ((string firstName, int day) in new[] { ("Ana", 1), ("Bruno", 5), ("Carla", 10), ("Davi", 15) })
        {
            context.Users.Single(u => u.FirstName == firstName).CreatedAtUtc = new DateTime(2026, 10, day, 12, 0, 0, DateTimeKind.Utc);
        }
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());

        // Act
        Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
            AllUsers with { SortBy = sortBy, SortDirection = sortDirection },
            CancellationToken.None);

        // Assert
        result.Value.Items.Select(u => u.FirstName).ShouldBe(expectedFirstNames);
    }

    [Theory]
    [InlineData(UserSortField.Name, SortDirection.Ascending)]
    [InlineData(UserSortField.Name, SortDirection.Descending)]
    [InlineData(UserSortField.CreatedAt, SortDirection.Ascending)]
    [InlineData(UserSortField.CreatedAt, SortDirection.Descending)]
    public async Task Handle_Should_BreakTiesById_WhenSortKeysAreEqual(UserSortField sortBy, SortDirection sortDirection)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var createdAtUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        User first = AddUser(context, "Ana", "ana1@tickestpristine.dev", isActive: true);
        User second = AddUser(context, "Ana", "ana2@tickestpristine.dev", isActive: true);
        first.CreatedAtUtc = createdAtUtc;
        second.CreatedAtUtc = createdAtUtc;
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context, CreateClock());
        Guid[] expected = sortDirection == SortDirection.Ascending
            ? [.. new[] { first.Id, second.Id }.Order()]
            : [.. new[] { first.Id, second.Id }.OrderDescending()];

        // Act
        var pages = new List<Guid>();
        for (int page = 1; page <= 2; page++)
        {
            Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(
                AllUsers with { SortBy = sortBy, SortDirection = sortDirection, Paging = new PageRequest(page, 1) },
                CancellationToken.None);
            pages.Add(result.Value.Items.ShouldHaveSingleItem().Id);
        }

        // Assert
        pages.ShouldBe(expected);
    }

    /// <summary>
    /// Ana (Atendente), Bruno (Atendente e Administrador), Carla (Colaboradora, inativa) e Davi (sem role).
    /// </summary>
    private static async Task<(User Ana, User Bruno, User Carla, User Davi)> SeedUsersWithRolesAsync(TestDbContext context)
    {
        User ana = AddUser(context, "Ana", "ana@tickestpristine.dev", isActive: true);
        User bruno = AddUser(context, "Bruno", "bruno@tickestpristine.dev", isActive: true);
        User carla = AddUser(context, "Carla", "carla@tickestpristine.dev", isActive: false);
        User davi = AddUser(context, "Davi", "davi@tickestpristine.dev", isActive: true);
        context.Roles.AddRange(CreateRole("Atendente"), CreateRole("Administrador"), CreateRole("Colaborador"));
        context.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), UserId = ana.Id, RoleName = "Atendente" },
            new UserRole { Id = Guid.NewGuid(), UserId = bruno.Id, RoleName = "Atendente" },
            new UserRole { Id = Guid.NewGuid(), UserId = bruno.Id, RoleName = "Administrador" },
            new UserRole { Id = Guid.NewGuid(), UserId = carla.Id, RoleName = "Colaborador" });
        await context.SaveChangesAsync();

        return (ana, bruno, carla, davi);
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
