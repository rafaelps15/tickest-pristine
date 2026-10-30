using TickestPristine.Application.Sectors.Deactivate;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class DeactivateSectorCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new DeactivateSectorCommandHandler(context);
        var command = new DeactivateSectorCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NotFound(command.SectorId));
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyDeactivated_WhenSectorIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        sector.Deactivate();
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();
        sector.ClearDomainEvents();

        var handler = new DeactivateSectorCommandHandler(context);
        var command = new DeactivateSectorCommand(sector.Id);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.AlreadyDeactivated(sector.Id));
        sector.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_DeactivateSector_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var handler = new DeactivateSectorCommandHandler(context);
        var command = new DeactivateSectorCommand(sector.Id);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Sector deactivated = await context.Sectors.SingleAsync(s => s.Id == sector.Id);
        deactivated.IsActive.ShouldBeFalse();
        deactivated.DomainEvents.ShouldContain(domainEvent => domainEvent is SectorDeactivatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenSectorHasActiveTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        context.Sectors.Add(sector);
        context.Tickets.Add(TicketTestData.Create(
            sector: sector,
            status: TicketStatus.InProgress,
            description: "The printer on the second floor is not working"));
        await context.SaveChangesAsync();

        var handler = new DeactivateSectorCommandHandler(context);

        // Act
        Result result = await handler.Handle(new DeactivateSectorCommand(sector.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.HasActiveTickets());

        Sector unchanged = await context.Sectors.SingleAsync(s => s.Id == sector.Id);
        unchanged.IsActive.ShouldBeTrue();
    }
}
