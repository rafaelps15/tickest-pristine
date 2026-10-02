using TickestPristine.Application.Sectors.GetById;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class GetSectorByIdQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new GetSectorByIdQueryHandler(context);
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
        await using TestDbContext context = CreateDbContext();
        var sector = new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", DepartmentId = Guid.NewGuid(), IsActive = true };
        sector.IsActive = false;
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var handler = new GetSectorByIdQueryHandler(context);
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
        await using TestDbContext context = CreateDbContext();
        var departmentId = Guid.NewGuid();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Name = "Helpdesk",
            DepartmentId = departmentId,
            Description = "Support requests",
            IsActive = true
        };
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var handler = new GetSectorByIdQueryHandler(context);
        var query = new GetSectorByIdQuery(sector.Id);

        // Act
        Result<SectorResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Helpdesk");
        result.Value.DepartmentId.ShouldBe(departmentId);
    }
}
