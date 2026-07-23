using TickestPristine.Application.Departments.Update;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class UpdateDepartmentCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new UpdateDepartmentCommandHandler(context);
        var command = new UpdateDepartmentCommand
        {
            DepartmentId = Guid.NewGuid(),
            Name = "Support",
            Description = "Updated description"
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Departments.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenResponsibleUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = Department.Create("Support", "Customer support department");
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new UpdateDepartmentCommandHandler(context);
        var command = new UpdateDepartmentCommand
        {
            DepartmentId = department.Id,
            Name = "Support",
            Description = "Updated description",
            ResponsibleUserId = Guid.NewGuid()
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.NotFound");
    }

    [Fact]
    public async Task Handle_Should_UpdateDepartment_WhenCommandIsValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = Department.Create("Support", "Customer support department");
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new UpdateDepartmentCommandHandler(context);
        var command = new UpdateDepartmentCommand
        {
            DepartmentId = department.Id,
            Name = "Customer Support",
            Description = "Updated description"
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Department updated = await context.Departments.SingleAsync(d => d.Id == department.Id);
        updated.Name.ShouldBe("Customer Support");
        updated.Description.ShouldBe("Updated description");
    }
}
