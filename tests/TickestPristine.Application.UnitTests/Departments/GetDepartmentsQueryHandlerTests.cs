using TickestPristine.Application.Departments.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class GetDepartmentsQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveDepartments_WithTheirActiveSectors()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var activeDepartment = Department.Create("IT", "Information Technology");
        var inactiveDepartment = Department.Create("Retired", "No longer used");
        inactiveDepartment.Deactivate();

        var activeSector = Sector.Create("Helpdesk", activeDepartment.Id);
        var inactiveSector = Sector.Create("Old Sector", activeDepartment.Id);
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
        response.Sectors.ShouldContain(s => s.Id == activeSector.Id);
        response.Sectors.ShouldNotContain(s => s.Id == inactiveSector.Id);
    }
}
