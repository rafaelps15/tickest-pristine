using TickestPristine.Application.Departments.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class GetDepartmentsQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveDepartmentsWithActiveSectors_WhenSomeAreInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var activeDepartment = Department.Create("IT", "Information Technology", responsibleUser: null);
        var inactiveDepartment = Department.Create("Retired", "No longer used", responsibleUser: null);
        inactiveDepartment.Deactivate();

        var activeSector = Sector.Create(activeDepartment, "Helpdesk", "Everyday support");
        var inactiveSector = Sector.Create(activeDepartment, "Old Sector", description: null);
        inactiveSector.Deactivate();

        context.Departments.AddRange(activeDepartment, inactiveDepartment);
        context.Sectors.AddRange(activeSector, inactiveSector);
        await context.SaveChangesAsync();

        var handler = new GetDepartmentsQueryHandler(context);

        // Act
        Result<List<DepartmentResponse>> result = await handler.Handle(new GetDepartmentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(d => d.Id == activeDepartment.Id);
        result.Value.ShouldNotContain(d => d.Id == inactiveDepartment.Id);

        DepartmentResponse response = result.Value.Single(d => d.Id == activeDepartment.Id);
        DepartmentSectorResponse sector = response.Sectors.ShouldHaveSingleItem();
        sector.Id.ShouldBe(activeSector.Id);
        sector.Name.ShouldBe("Helpdesk");
        sector.Description.ShouldBe("Everyday support");
    }
}
