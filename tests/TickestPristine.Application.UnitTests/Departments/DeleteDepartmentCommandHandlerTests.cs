using TickestPristine.Application.Departments.Delete;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class DeleteDepartmentCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new DeleteDepartmentCommandHandler(context);
        var command = new DeleteDepartmentCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Departments.NotFound");
    }

    [Fact]
    public async Task Handle_Should_DeactivateDepartment_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = Department.Create("Support", "Customer support department");
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new DeleteDepartmentCommandHandler(context);
        var command = new DeleteDepartmentCommand(department.Id);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Department deactivated = await context.Departments.SingleAsync(d => d.Id == department.Id);
        deactivated.IsActive.ShouldBeFalse();
    }
}
