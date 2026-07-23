using TickestPristine.Application.Sectors.Update;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

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
        result.Error.Code.ShouldBe("Sectors.NotFound");
    }

    [Fact]
    public async Task Handle_Should_UpdateSector_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var sector = Sector.Create("Helpdesk", Guid.NewGuid(), "Original description");
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
    }
}
