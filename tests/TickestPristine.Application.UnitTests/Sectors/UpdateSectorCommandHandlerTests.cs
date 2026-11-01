using TickestPristine.Application.Sectors.Update;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class UpdateSectorCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new UpdateSectorCommandHandler(context);
        var command = new UpdateSectorCommand { SectorId = Guid.NewGuid(), Name = "Helpdesk" };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NotFound(command.SectorId));
    }

    [Fact]
    public async Task Handle_Should_UpdateSector_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", "Original description");
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var handler = new UpdateSectorCommandHandler(context);
        var command = new UpdateSectorCommand { SectorId = sector.Id, Name = "Technical Support", Description = "Updated" };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Sector updated = await context.Sectors.SingleAsync(s => s.Id == sector.Id);
        updated.Name.ShouldBe("Technical Support");
        updated.Description.ShouldBe("Updated");
        updated.DomainEvents.ShouldContain(domainEvent => domainEvent is SectorUpdatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenAnotherActiveSectorOfTheDepartmentHasTheName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = Department.Create("Suporte", "Departamento de teste", responsibleUser: null);
        var sector = Sector.Create(department, "Helpdesk", description: null);
        context.Sectors.Add(sector);
        context.Sectors.Add(Sector.Create(department, "Networks", description: null));
        await context.SaveChangesAsync();

        var handler = new UpdateSectorCommandHandler(context);
        var command = new UpdateSectorCommand { SectorId = sector.Id, Name = "Networks" };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NameNotUnique(command.Name));
    }
}
