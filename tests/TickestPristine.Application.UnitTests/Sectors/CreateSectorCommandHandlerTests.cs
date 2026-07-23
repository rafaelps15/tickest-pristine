using TickestPristine.Application.Sectors.Create;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class CreateSectorCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new CreateSectorCommandHandler(context);
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = Guid.NewGuid() };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Departments.NotFound");
    }

    [Fact]
    public async Task Handle_Should_CreateSector_WhenDepartmentExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = Department.Create("Support", "Customer support department");
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new CreateSectorCommandHandler(context);
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = department.Id };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Sector sector = await context.Sectors.SingleAsync(s => s.Id == result.Value);
        sector.Name.ShouldBe("Helpdesk");
        sector.DepartmentId.ShouldBe(department.Id);
        sector.IsActive.ShouldBeTrue();
    }
}
