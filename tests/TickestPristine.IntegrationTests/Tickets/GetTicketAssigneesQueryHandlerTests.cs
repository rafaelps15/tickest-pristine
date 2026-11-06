using TickestPristine.Application.Users;
using TickestPristine.Application.Users.GetAttendants;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets.GetAssignees;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Tickets;

public sealed class GetTicketAssigneesQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveUsersWithManagePermission_WhenSomeUsersAreNotEligible()
    {
        // Arrange: Ana e Beto ativos com tickets:manage, Caio inativo com tickets:manage e Davi ativo sem tickets:manage
        await using ApplicationDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context, "Helpdesk", "Tecnologia");
        User ana = await AddUserAsync(context, "Ana", sector.Id, isActive: true, PermissionCodes.Tickets.Manage);
        User beto = await AddUserAsync(context, "Beto", null, isActive: true, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Caio", sector.Id, isActive: false, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Davi", sector.Id, isActive: true, PermissionCodes.Tickets.Create);

        var handler = new GetTicketAssigneesQueryHandler(DbConnectionFactory);

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
        await using ApplicationDbContext context = CreateDbContext();
        Sector helpdesk = await SeedSectorAsync(context, "Helpdesk", "Tecnologia");
        Sector infra = await SeedSectorAsync(context, "Infra", "Operações");
        User mariana = await AddUserAsync(context, "Mariana", helpdesk.Id, isActive: true, PermissionCodes.Tickets.Manage);
        User ana = await AddUserAsync(context, "Ana", helpdesk.Id, isActive: true, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Anabela", infra.Id, isActive: true, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Beto", helpdesk.Id, isActive: true, PermissionCodes.Tickets.Manage);

        var handler = new GetTicketAssigneesQueryHandler(DbConnectionFactory);

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
        await using ApplicationDbContext context = CreateDbContext();
        User ana = await AddUserAsync(context, "Ana", null, isActive: true, PermissionCodes.Tickets.Manage);
        await AddUserAsync(context, "Beto", null, isActive: true, PermissionCodes.Tickets.Manage);

        var handler = new GetTicketAssigneesQueryHandler(DbConnectionFactory);

        // Act
        Result<PagedResponse<TicketAssigneeResponse>> result = await handler.Handle(
            CreateQuery(search: "Ana Silva"),
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(ana.Id);
    }

    [Fact]
    public async Task Handle_Should_ListTheSameAgentsAsAttendantsAndAgentLookup_WhenUsersHaveEveryKindOfAccess()
    {
        // Arrange: Ana com uma role de tickets:manage, Beto com duas roles (só uma com tickets:manage), Caio inativo com
        // tickets:manage, Davi com role sem tickets:manage, Eva com uma role sem permissões e Fábio pendente (sem senha) com
        // tickets:manage, que hoje também atende
        await using ApplicationDbContext context = CreateDbContext();
        Sector sector = await SeedSectorAsync(context, "Helpdesk", "Tecnologia");
        User ana = await AddUserAsync(context, "Ana", sector.Id, isActive: true, PermissionCodes.Tickets.Manage);
        User beto = await AddUserAsync(context, "Beto", null, isActive: true, PermissionCodes.Tickets.Create);
        await GrantPermissionsAsync(context, beto.Id, PermissionCodes.Tickets.Manage);
        User caio = await AddUserAsync(context, "Caio", sector.Id, isActive: false, PermissionCodes.Tickets.Manage);
        User davi = await AddUserAsync(context, "Davi", sector.Id, isActive: true, PermissionCodes.Tickets.Create);
        User eva = await AddUserAsync(context, "Eva", null, isActive: true);
        User fabio = await AddUserAsync(context, "Fábio", sector.Id, isActive: true, PermissionCodes.Tickets.Manage);
        context.UserCredentials.AddRange(
            new[] { ana, beto, caio, davi, eva }.Select(user => new UserCredential { Id = Guid.NewGuid(), UserId = user.Id, PasswordHash = "hash" }));
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Users.Read, Arg.Any<CancellationToken>())
            .Returns(true);
        var assignees = new GetTicketAssigneesQueryHandler(DbConnectionFactory);
        var attendants = new GetBacklogAttendantsQueryHandler(DbConnectionFactory, userContext, permissionProvider);
        var agentDirectory = new AgentDirectory(context);

        // Act
        Result<PagedResponse<TicketAssigneeResponse>> assigneePage = await assignees.Handle(
            new GetTicketAssigneesQuery(null, null, new PageRequest(1, 100)),
            CancellationToken.None);
        Result<PagedResponse<AttendantResponse>> attendantPage = await attendants.Handle(
            new GetBacklogAttendantsQuery(null, null, null, new PageRequest(1, 100)),
            CancellationToken.None);
        List<Guid> lookedUp = [];
        foreach (User user in new[] { ana, beto, caio, davi, eva, fabio })
        {
            if (await agentDirectory.IsAgentAsync(user.Id, CancellationToken.None))
            {
                lookedUp.Add(user.Id);
            }
        }

        // Assert
        Guid[] expected = [ana.Id, beto.Id, fabio.Id];
        assigneePage.Value.Items.Select(user => user.Id).ShouldBe(expected, ignoreOrder: true);
        attendantPage.Value.Items.Select(user => user.Id).ShouldBe(expected, ignoreOrder: true);
        lookedUp.ShouldBe(expected, ignoreOrder: true);
    }

    private static GetTicketAssigneesQuery CreateQuery(string? search = null, Guid? sectorId = null) =>
        new(search, sectorId, new PageRequest(1, 20));

    private static async Task<User> AddUserAsync(
        ApplicationDbContext context,
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
