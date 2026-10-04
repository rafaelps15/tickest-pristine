using TickestPristine.Application.Departments.Update;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.Users;

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
        result.Error.ShouldBe(DepartmentErrors.NotFound(command.DepartmentId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenResponsibleUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
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
        result.Error.ShouldBe(UserErrors.NotFound(command.ResponsibleUserId.Value));
    }

    [Fact]
    public async Task Handle_Should_UpdateDepartment_WhenCommandIsValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
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
        updated.DomainEvents.ShouldContain(domainEvent => domainEvent is DepartmentUpdatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenAnotherActiveDepartmentHasTheName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
        context.Departments.Add(department);
        context.Departments.Add(new Department { Id = Guid.NewGuid(), Name = "Finance", Description = "Finance department", IsActive = true });
        await context.SaveChangesAsync();

        var handler = new UpdateDepartmentCommandHandler(context);
        var command = new UpdateDepartmentCommand
        {
            DepartmentId = department.Id,
            Name = "Finance",
            Description = "Updated description"
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DepartmentErrors.NameNotUnique(command.Name));
    }
}
