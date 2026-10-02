using TickestPristine.Application.Sectors.Deactivate;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

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
    public async Task Handle_Should_DeactivateSector_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var sector = new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", DepartmentId = Guid.NewGuid(), IsActive = true };
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
}
