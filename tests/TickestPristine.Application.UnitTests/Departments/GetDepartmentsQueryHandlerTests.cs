using TickestPristine.Application.Departments.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class GetDepartmentsQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveDepartmentsWithActiveSectors_WhenSomeAreInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var activeDepartment = new Department { Id = Guid.NewGuid(), Name = "IT", Description = "Information Technology", IsActive = true };
        var inactiveDepartment = new Department { Id = Guid.NewGuid(), Name = "Retired", Description = "No longer used", IsActive = true };
        inactiveDepartment.IsActive = false;

        var activeSector = new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", Description = "Everyday support", DepartmentId = activeDepartment.Id, IsActive = true };
        var inactiveSector = new Sector { Id = Guid.NewGuid(), Name = "Old Sector", DepartmentId = activeDepartment.Id, IsActive = true };
        inactiveSector.IsActive = false;

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
