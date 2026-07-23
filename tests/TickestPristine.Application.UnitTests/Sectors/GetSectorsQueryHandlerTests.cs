using TickestPristine.Application.Sectors.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class GetSectorsQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveSectors()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var activeSector = Sector.Create("Helpdesk", Guid.NewGuid());
        var inactiveSector = Sector.Create("Retired Sector", Guid.NewGuid());
        inactiveSector.Deactivate();

        context.Sectors.AddRange(activeSector, inactiveSector);
        await context.SaveChangesAsync();

        var handler = new GetSectorsQueryHandler(context);

        // Act
        Result<List<SectorResponse>> result = await handler.Handle(new GetSectorsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(s => s.Id == activeSector.Id);
        result.Value.ShouldNotContain(s => s.Id == inactiveSector.Id);
    }
}
