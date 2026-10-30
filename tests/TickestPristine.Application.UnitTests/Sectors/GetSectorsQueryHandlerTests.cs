using TickestPristine.Application.Sectors.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class GetSectorsQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveSectors_WhenSomeAreInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var activeSector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        var inactiveSector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Retired Sector", description: null);
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
