using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.GetAttendants;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetBacklogAttendantsQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid CallerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnReadForbidden_WhenCallerLacksReadPermission()
    {
        // Arrange: quem chama atende a fila, mas não pode ver usuários
        await using TestDbContext context = CreateDbContext();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(CallerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        GetBacklogAttendantsQueryHandler handler = CreateHandler(context, permissionProvider);

        // Act
        Result<PagedResponse<AttendantResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.ReadForbidden);
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" a ")]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Handle_Should_ReturnInvalidSearchTerm_WhenTrimmedTermIsOutOfRange(string searchTerm)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetBacklogAttendantsQueryHandler handler = CreateHandler(context, PermissionProviderWithRead());

        // Act
        Result<PagedResponse<AttendantResponse>> result = await handler.Handle(
            CreateQuery(searchTerm: searchTerm),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidSearchTerm(2, 100));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" Ana ")]
    [InlineData("  aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  ")]
    public async Task Handle_Should_AcceptSearchTerm_WhenTermIsAbsentOrTrimmedTermHasValidLength(string? searchTerm)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetBacklogAttendantsQueryHandler handler = CreateHandler(context, PermissionProviderWithRead());

        // Act
        Result<PagedResponse<AttendantResponse>> result = await handler.Handle(
            CreateQuery(searchTerm: searchTerm),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_IgnoreWordsAfterTheFifth()
    {
        // Arrange: as cinco primeiras palavras estão nos dados de Ana; a sexta não está em ninguém
        await using TestDbContext context = CreateDbContext();
        (Guid sectorId, _) = await SeedSectorAsync(context, "TI", "Tecnologia");
        User ana = await SeedUserAsync(context, "Ana", "Souza", sectorId, PermissionCodes.Tickets.Manage);
        GetBacklogAttendantsQueryHandler handler = CreateHandler(context, PermissionProviderWithRead());

        // Act
        Result<PagedResponse<AttendantResponse>> result = await handler.Handle(
            CreateQuery(searchTerm: "Ana Souza TI Tecnologia Souza Inexistente"),
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(ana.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveUsersWithManagePermissionOrderedByName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (Guid sectorId, _) = await SeedSectorAsync(context, "TI", "Tecnologia");
        User beto = await SeedUserAsync(context, "Beto", "Lima", sectorId, PermissionCodes.Tickets.Manage);
        User ana = await SeedUserAsync(context, "Ana", "Souza", sectorId, PermissionCodes.Tickets.Manage);
        User eva = await SeedUserAsync(context, "Eva", "Rocha", null, PermissionCodes.Tickets.Manage);
        await SeedUserAsync(context, "Bruno", "Alves", sectorId, PermissionCodes.Tickets.Manage, isActive: false);
        await SeedUserAsync(context, "Carla", "Dias", sectorId, PermissionCodes.Tickets.Create);

        GetBacklogAttendantsQueryHandler handler = CreateHandler(context, PermissionProviderWithRead());

        // Act
        Result<PagedResponse<AttendantResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(3);
        result.Value.Items.Select(a => a.Id).ShouldBe([ana.Id, beto.Id, eva.Id]);

        AttendantResponse anaItem = result.Value.Items[0];
        anaItem.FullName.ShouldBe("Ana Souza");
        anaItem.Email.ShouldBe(ana.Email);
        anaItem.SectorId.ShouldBe(sectorId);
        anaItem.SectorName.ShouldBe("TI");
        anaItem.DepartmentName.ShouldBe("Tecnologia");

        AttendantResponse evaItem = result.Value.Items[2];
        evaItem.SectorId.ShouldBeNull();
        evaItem.SectorName.ShouldBeNull();
        evaItem.DepartmentName.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_PutPrioritySectorFirst_WhenPrioritySectorIdIsGiven()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (Guid itSectorId, _) = await SeedSectorAsync(context, "TI", "Tecnologia");
        (Guid financeSectorId, _) = await SeedSectorAsync(context, "Financeiro", "Administrativo");
        User ana = await SeedUserAsync(context, "Ana", "Souza", itSectorId, PermissionCodes.Tickets.Manage);
        User caio = await SeedUserAsync(context, "Caio", "Melo", financeSectorId, PermissionCodes.Tickets.Manage);
        User eva = await SeedUserAsync(context, "Eva", "Rocha", null, PermissionCodes.Tickets.Manage);

        GetBacklogAttendantsQueryHandler handler = CreateHandler(context, PermissionProviderWithRead());

        // Act
        Result<PagedResponse<AttendantResponse>> result = await handler.Handle(
            CreateQuery(prioritySectorId: financeSectorId),
            CancellationToken.None);

        // Assert
        result.Value.Items.Select(a => a.Id).ShouldBe([caio.Id, ana.Id, eva.Id]);
    }

    [Fact]
    public async Task Handle_Should_FilterBySectorAndEveryWord_WhenFiltersAreCombined()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (Guid itSectorId, _) = await SeedSectorAsync(context, "TI", "Tecnologia");
        (Guid financeSectorId, _) = await SeedSectorAsync(context, "Financeiro", "Administrativo");
        User ana = await SeedUserAsync(context, "Ana", "Souza", itSectorId, PermissionCodes.Tickets.Manage);
        await SeedUserAsync(context, "Beto", "Lima", itSectorId, PermissionCodes.Tickets.Manage);
        await SeedUserAsync(context, "Caio", "Melo", financeSectorId, PermissionCodes.Tickets.Manage);

        GetBacklogAttendantsQueryHandler handler = CreateHandler(context, PermissionProviderWithRead());

        // Act
        Result<PagedResponse<AttendantResponse>> fullName = await handler.Handle(
            CreateQuery(searchTerm: " Ana  Souza "),
            CancellationToken.None);
        Result<PagedResponse<AttendantResponse>> wordsOfDifferentUsers = await handler.Handle(
            CreateQuery(searchTerm: "Souza Lima"),
            CancellationToken.None);
        Result<PagedResponse<AttendantResponse>> otherSector = await handler.Handle(
            CreateQuery(searchTerm: "Caio", sectorId: itSectorId),
            CancellationToken.None);

        // Assert
        fullName.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(ana.Id);
        wordsOfDifferentUsers.Value.Items.ShouldBeEmpty();
        otherSector.Value.Items.ShouldBeEmpty();
        otherSector.Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_FindBySectorAndDepartmentName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (Guid itSectorId, _) = await SeedSectorAsync(context, "TI", "Tecnologia");
        User ana = await SeedUserAsync(context, "Ana", "Souza", itSectorId, PermissionCodes.Tickets.Manage);
        await SeedUserAsync(context, "Eva", "Rocha", null, PermissionCodes.Tickets.Manage);

        GetBacklogAttendantsQueryHandler handler = CreateHandler(context, PermissionProviderWithRead());

        // Act
        Result<PagedResponse<AttendantResponse>> byDepartment = await handler.Handle(
            CreateQuery(searchTerm: "Tecnolog"),
            CancellationToken.None);
        Result<PagedResponse<AttendantResponse>> bySector = await handler.Handle(
            CreateQuery(searchTerm: "TI"),
            CancellationToken.None);

        // Assert
        byDepartment.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(ana.Id);
        bySector.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(ana.Id);
    }

    private static GetBacklogAttendantsQuery CreateQuery(
        string? searchTerm = null,
        Guid? sectorId = null,
        Guid? prioritySectorId = null) =>
        new(searchTerm, sectorId, prioritySectorId, new PageRequest(null, null));

    private static IPermissionProvider PermissionProviderWithRead()
    {
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(CallerId, PermissionCodes.Users.Read, Arg.Any<CancellationToken>())
            .Returns(true);

        return permissionProvider;
    }

    private static GetBacklogAttendantsQueryHandler CreateHandler(TestDbContext context, IPermissionProvider permissionProvider)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CallerId);

        return new GetBacklogAttendantsQueryHandler(context, userContext, permissionProvider);
    }

    private static async Task<User> SeedUserAsync(
        TestDbContext context,
        string firstName,
        string lastName,
        Guid? sectorId,
        string permissionCode,
        bool isActive = true)
    {
        User user = CreateUser(isActive);
        user.FirstName = firstName;
        user.LastName = lastName;
        user.SectorId = sectorId;

        context.Users.Add(user);
        await context.SaveChangesAsync();
        await GrantPermissionsAsync(context, user.Id, permissionCode);

        return user;
    }

    private static async Task<(Guid SectorId, Guid DepartmentId)> SeedSectorAsync(
        TestDbContext context,
        string sectorName,
        string departmentName)
    {
        var department = Department.Create(departmentName, departmentName, responsibleUser: null);
        var sector = Sector.Create(department, sectorName, description: null);

        context.Departments.Add(department);
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return (sector.Id, department.Id);
    }
}
