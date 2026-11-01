using TickestPristine.Application.Departments.Update;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Domain.Users;
using TickestPristine.Application.Users;

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
        var department = Department.Create("Support", "Customer support department", responsibleUser: null);
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
        var department = Department.Create("Support", "Customer support department", responsibleUser: null);
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
    public async Task Handle_Should_UpdateDepartment_WhenResponsibleUserExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = Department.Create("Support", "Customer support department", responsibleUser: null);
        var responsibleUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "responsible@tickestpristine.dev",
            FirstName = "Resp",
            LastName = "User",
            Code = UserCodes.New()
        };
        context.Departments.Add(department);
        context.Users.Add(responsibleUser);
        await context.SaveChangesAsync();

        var handler = new UpdateDepartmentCommandHandler(context);
        var command = new UpdateDepartmentCommand
        {
            DepartmentId = department.Id,
            Name = "Support",
            Description = "Updated description",
            ResponsibleUserId = responsibleUser.Id
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Department updated = await context.Departments.SingleAsync(d => d.Id == department.Id);
        updated.ResponsibleUserId.ShouldBe(responsibleUser.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenAnotherActiveDepartmentHasTheName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var department = Department.Create("Support", "Customer support department", responsibleUser: null);
        context.Departments.Add(department);
        context.Departments.Add(Department.Create("Finance", "Finance department", responsibleUser: null));
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
