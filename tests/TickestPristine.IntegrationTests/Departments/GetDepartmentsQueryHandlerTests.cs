using TickestPristine.Application.Departments.GetAll;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Application.Users;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Departments;

public sealed class GetDepartmentsQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnResponsibleIdAndName_WhenDepartmentHasResponsible()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();

        var ana = new User
        {
            Id = Guid.NewGuid(),
            Email = "ana@tickestpristine.dev",
            FirstName = "Ana",
            LastName = "Souza",
            Code = UserCodes.New()
        };
        var it = Department.Create("IT", "Information Technology", responsibleUser: ana);
        var finance = Department.Create("Finance", "Finance department", responsibleUser: null);

        context.Users.Add(ana);
        context.Departments.AddRange(it, finance);
        await context.SaveChangesAsync();

        var handler = new GetDepartmentsQueryHandler(DbConnectionFactory);

        // Act
        Result<List<DepartmentResponse>> result = await handler.Handle(new GetDepartmentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        DepartmentResponse itResponse = result.Value.Single(d => d.Id == it.Id);
        itResponse.ResponsibleUserId.ShouldBe(ana.Id);
        itResponse.ResponsibleUserName.ShouldBe("Ana Souza");

        DepartmentResponse financeResponse = result.Value.Single(d => d.Id == finance.Id);
        financeResponse.ResponsibleUserId.ShouldBeNull();
        financeResponse.ResponsibleUserName.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveDepartmentsWithActiveSectors_WhenSomeAreInactive()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();

        var activeDepartment = Department.Create("IT", "Information Technology", responsibleUser: null);
        var inactiveDepartment = Department.Create("Retired", "No longer used", responsibleUser: null);
        inactiveDepartment.Deactivate();

        var activeSector = Sector.Create(activeDepartment, "Helpdesk", "Everyday support");
        var inactiveSector = Sector.Create(activeDepartment, "Old Sector", description: null);
        inactiveSector.Deactivate();

        context.Departments.AddRange(activeDepartment, inactiveDepartment);
        context.Sectors.AddRange(activeSector, inactiveSector);
        await context.SaveChangesAsync();

        var handler = new GetDepartmentsQueryHandler(DbConnectionFactory);

        // Act
        Result<List<DepartmentResponse>> result = await handler.Handle(new GetDepartmentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(d => d.Id == activeDepartment.Id);
        result.Value.ShouldNotContain(d => d.Id == inactiveDepartment.Id);

        DepartmentResponse response = result.Value.Single(d => d.Id == activeDepartment.Id);
        DepartmentSectorResponse sector = response.Sectors.ShouldHaveSingleItem();
        sector.Id.ShouldBe(activeSector.Id);
        sector.Name.ShouldBe("Helpdesk");
        sector.Description.ShouldBe("Everyday support");
    }

    [Fact]
    public async Task Handle_Should_ReturnActiveTicketCountOfEachDepartment_WhenSectorsHaveTicketsInSeveralStatuses()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        CommonData data = await SeedCommonDataAsync(context);
        var handler = new GetDepartmentsQueryHandler(DbConnectionFactory);

        // Act
        Result<List<DepartmentResponse>> result = await handler.Handle(new GetDepartmentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Single(d => d.Id == data.It.Id).ActiveTicketCount.ShouldBe(3);
        result.Value.Single(d => d.Id == data.Finance.Id).ActiveTicketCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnActiveTicketCountOfEachSector_WhenSectorsHaveTicketsInSeveralStatuses()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        CommonData data = await SeedCommonDataAsync(context);
        var handler = new GetDepartmentsQueryHandler(DbConnectionFactory);

        // Act
        Result<List<DepartmentResponse>> result = await handler.Handle(new GetDepartmentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        List<DepartmentSectorResponse> sectors = result.Value.Single(d => d.Id == data.It.Id).Sectors;
        sectors.Single(s => s.Id == data.Support.Id).ActiveTicketCount.ShouldBe(3);
        sectors.Single(s => s.Id == data.Networks.Id).ActiveTicketCount.ShouldBe(0);
        sectors.Single(s => s.Id == data.Infrastructure.Id).ActiveTicketCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_IncludeDeactivatedSectorsInDepartmentCount_WhenTheyHaveActiveTickets()
    {
        // Arrange: um chamado ativo num setor desativado, que a API produz quando um chamado do setor é reaberto
        // depois da desativação.
        await using ApplicationDbContext context = CreateDbContext();
        var department = Department.Create("TI", "Tecnologia da informação", responsibleUser: null);
        var activeSector = Sector.Create(department, "Suporte", description: null);
        var inactiveSector = Sector.Create(department, "Legado", description: null);
        Ticket ticketOfInactiveSector = QueryTicketTestData.Create(sector: inactiveSector, createdAtUtc: OpenedAtUtc);
        inactiveSector.Deactivate();

        context.Departments.Add(department);
        context.Sectors.AddRange(activeSector, inactiveSector);
        context.Tickets.AddRange(QueryTicketTestData.Create(sector: activeSector, createdAtUtc: OpenedAtUtc), ticketOfInactiveSector);
        await context.SaveChangesAsync();

        var handler = new GetDepartmentsQueryHandler(DbConnectionFactory);

        // Act
        Result<List<DepartmentResponse>> result = await handler.Handle(new GetDepartmentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DepartmentResponse response = result.Value.Single(d => d.Id == department.Id);
        response.ActiveTicketCount.ShouldBe(2);
        response.Sectors.ShouldHaveSingleItem().ActiveTicketCount.ShouldBe(1);
    }

    // TI com Suporte (2 Abertos e 1 Em andamento), Redes (1 Resolvido, 1 Cancelado e 1 Aberto excluído) e Infra, sem
    // chamados; Financeiro, sem setores.
    private static async Task<CommonData> SeedCommonDataAsync(ApplicationDbContext context)
    {
        var it = Department.Create("TI", "Tecnologia da informação", responsibleUser: null);
        var finance = Department.Create("Financeiro", "Contas a pagar e a receber", responsibleUser: null);
        var support = Sector.Create(it, "Suporte", description: null);
        var networks = Sector.Create(it, "Redes", description: null);
        var infrastructure = Sector.Create(it, "Infra", description: null);

        context.Departments.AddRange(it, finance);
        context.Sectors.AddRange(support, networks, infrastructure);
        context.Tickets.AddRange(
            QueryTicketTestData.Create(sector: support, status: TicketStatus.Open, createdAtUtc: OpenedAtUtc),
            QueryTicketTestData.Create(sector: support, status: TicketStatus.Open, createdAtUtc: OpenedAtUtc),
            QueryTicketTestData.Create(sector: support, status: TicketStatus.InProgress, createdAtUtc: OpenedAtUtc),
            QueryTicketTestData.Create(sector: networks, status: TicketStatus.Resolved, createdAtUtc: OpenedAtUtc),
            QueryTicketTestData.Create(sector: networks, status: TicketStatus.Canceled, createdAtUtc: OpenedAtUtc),
            QueryTicketTestData.Create(
                sector: networks,
                status: TicketStatus.Open,
                createdAtUtc: OpenedAtUtc,
                deletedAtUtc: OpenedAtUtc.AddHours(1)));
        await context.SaveChangesAsync();

        return new CommonData(it, finance, support, networks, infrastructure);
    }

    private sealed record CommonData(
        Department It,
        Department Finance,
        Sector Support,
        Sector Networks,
        Sector Infrastructure);
}
