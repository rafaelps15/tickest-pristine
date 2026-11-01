using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets.GetAssignees;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketAssigneesQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveUsersWithManagePermission_WhenSomeUsersAreNotEligible()
    {
        // Arrange: Ana e Beto ativos com tickets:manage, Caio inativo com tickets:manage e Davi ativo sem tickets:manage
        await using TestDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context, "Helpdesk", "Tecnologia");
        User ana = await AddUserAsync(context, "Ana", sector.Id, isActive: true, PermissionCodes.Tickets.Manage);
        User beto = await AddUserAsync(context, "Beto", null, isActive: true, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Caio", sector.Id, isActive: false, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Davi", sector.Id, isActive: true, PermissionCodes.Tickets.Create);

        var handler = new GetTicketAssigneesQueryHandler(context);

        // Act
        Result<PagedResponse<TicketAssigneeResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(a => a.Id).ShouldBe([ana.Id, beto.Id]);

        TicketAssigneeResponse anaResponse = result.Value.Items[0];
        anaResponse.FullName.ShouldBe("Ana Silva");
        anaResponse.SectorId.ShouldBe(sector.Id);
        anaResponse.SectorName.ShouldBe("Helpdesk");

        TicketAssigneeResponse betoResponse = result.Value.Items[1];
        betoResponse.SectorId.ShouldBeNull();
        betoResponse.SectorName.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnMatchingAssigneesOrderedByName_WhenSearchAndSectorAreGiven()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Sector helpdesk = await SeedSectorAsync(context, "Helpdesk", "Tecnologia");
        Sector infra = await SeedSectorAsync(context, "Infra", "Operações");
        User mariana = await AddUserAsync(context, "Mariana", helpdesk.Id, isActive: true, PermissionCodes.Tickets.Manage);
        User ana = await AddUserAsync(context, "Ana", helpdesk.Id, isActive: true, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Anabela", infra.Id, isActive: true, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Beto", helpdesk.Id, isActive: true, PermissionCodes.Tickets.Manage);

        var handler = new GetTicketAssigneesQueryHandler(context);

        // Act
        Result<PagedResponse<TicketAssigneeResponse>> result = await handler.Handle(
            CreateQuery(search: " na ", sectorId: helpdesk.Id),
            CancellationToken.None);

        // Assert: "na" está em Ana e Mariana no Helpdesk; Anabela é de outro setor (maiúsculas e minúsculas são
        // conferidas no teste de integração, com o citext do PostgreSQL)
        result.Value.Items.Select(a => a.Id).ShouldBe([ana.Id, mariana.Id]);
        result.Value.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_RequireEveryWordInNameOrSurname_WhenSearchHasSeveralWords()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User ana = await AddUserAsync(context, "Ana", null, isActive: true, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Beto", null, isActive: true, PermissionCodes.Tickets.Manage);

        var handler = new GetTicketAssigneesQueryHandler(context);

        // Act
        Result<PagedResponse<TicketAssigneeResponse>> result = await handler.Handle(
            CreateQuery(search: "Ana Silva"),
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(ana.Id);
    }

    private static GetTicketAssigneesQuery CreateQuery(string? search = null, Guid? sectorId = null) =>
        new(search, sectorId, new PageRequest(1, 20));

    private static async Task<User> AddUserAsync(
        TestDbContext context,
        string firstName,
        Guid? sectorId,
        bool isActive,
        params string[] permissionCodes)
    {
        User user = CreateUser(isActive);
        user.FirstName = firstName;
        user.LastName = "Silva";
        user.SectorId = sectorId;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await GrantPermissionsAsync(context, user.Id, permissionCodes);

        return user;
    }
}
