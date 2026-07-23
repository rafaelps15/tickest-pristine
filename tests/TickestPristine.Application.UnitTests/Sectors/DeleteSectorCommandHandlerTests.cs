using TickestPristine.Application.Sectors.Delete;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class DeleteSectorCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new DeleteSectorCommandHandler(context);
        var command = new DeleteSectorCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Sectors.NotFound");
    }

    [Fact]
    public async Task Handle_Should_DeactivateSector_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var sector = Sector.Create("Helpdesk", Guid.NewGuid());
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var handler = new DeleteSectorCommandHandler(context);
        var command = new DeleteSectorCommand(sector.Id);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Sector deactivated = await context.Sectors.SingleAsync(s => s.Id == sector.Id);
        deactivated.IsActive.ShouldBeFalse();
    }
}
