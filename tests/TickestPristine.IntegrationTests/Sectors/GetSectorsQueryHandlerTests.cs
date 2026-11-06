using TickestPristine.Application.Sectors.GetAll;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Sectors;

public sealed class GetSectorsQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveSectors_WhenSomeAreInactive()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var activeSector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        var inactiveSector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Retired Sector", description: null);
        inactiveSector.Deactivate();

        context.Sectors.AddRange(activeSector, inactiveSector);
        await context.SaveChangesAsync();

        var handler = new GetSectorsQueryHandler(DbConnectionFactory);

        // Act
        Result<List<SectorResponse>> result = await handler.Handle(new GetSectorsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(s => s.Id == activeSector.Id);
        result.Value.ShouldNotContain(s => s.Id == inactiveSector.Id);
    }
}
