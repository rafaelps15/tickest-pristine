using TickestPristine.Application.Sectors.GetById;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Sectors;

public sealed class GetSectorByIdQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var handler = new GetSectorByIdQueryHandler(DbConnectionFactory);
        var query = new GetSectorByIdQuery(Guid.NewGuid());

        // Act
        Result<SectorResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NotFound(query.SectorId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectorIsInactive()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        sector.Deactivate();
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var handler = new GetSectorByIdQueryHandler(DbConnectionFactory);
        var query = new GetSectorByIdQuery(sector.Id);

        // Act
        Result<SectorResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NotFound(query.SectorId));
    }

    [Fact]
    public async Task Handle_Should_ReturnSectorWithDepartmentId_WhenActive()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var department = Department.Create("Suporte", "Departamento de teste", responsibleUser: null);
        var sector = Sector.Create(department, "Helpdesk", "Support requests");
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var handler = new GetSectorByIdQueryHandler(DbConnectionFactory);
        var query = new GetSectorByIdQuery(sector.Id);

        // Act
        Result<SectorResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Helpdesk");
        result.Value.DepartmentId.ShouldBe(department.Id);
    }
}
