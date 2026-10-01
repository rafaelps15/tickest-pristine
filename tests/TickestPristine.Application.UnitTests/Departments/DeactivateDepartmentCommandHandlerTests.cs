using TickestPristine.Application.Departments.Deactivate;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class DeactivateDepartmentCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new DeactivateDepartmentCommandHandler(context);
        var command = new DeactivateDepartmentCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DepartmentErrors.NotFound(command.DepartmentId));
    }

    [Fact]
    public async Task Handle_Should_DeactivateDepartment_WhenItExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = new Department { Id = Guid.NewGuid(), Name = "Support", Description = "Customer support department", IsActive = true };
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var handler = new DeactivateDepartmentCommandHandler(context);
        var command = new DeactivateDepartmentCommand(department.Id);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Department deactivated = await context.Departments.SingleAsync(d => d.Id == department.Id);
        deactivated.IsActive.ShouldBeFalse();
        deactivated.DomainEvents.ShouldContain(domainEvent => domainEvent is DepartmentDeactivatedDomainEvent);
    }
}
