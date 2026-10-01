using TickestPristine.Application.Departments.Create;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class CreateDepartmentCommandHandlerTests : BaseHandlerTest
{
    private static CreateDepartmentCommand Command => new()
    {
        Name = "Support",
        Description = "Customer support department"
    };

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenResponsibleUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new CreateDepartmentCommandHandler(context);
        CreateDepartmentCommand command = Command;
        command.ResponsibleUserId = Guid.NewGuid();

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.ResponsibleUserId.Value));
    }

    [Fact]
    public async Task Handle_Should_CreateDepartment_WhenCommandIsValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new CreateDepartmentCommandHandler(context);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Department department = await context.Departments.SingleAsync(d => d.Id == result.Value);
        department.Name.ShouldBe("Support");
        department.IsActive.ShouldBeTrue();
        department.DomainEvents.ShouldContain(domainEvent => domainEvent is DepartmentCreatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_CreateDepartment_WhenResponsibleUserExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var responsibleUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "responsible@tickestpristine.dev",
            FirstName = "Resp",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(responsibleUser);
        await context.SaveChangesAsync();

        var handler = new CreateDepartmentCommandHandler(context);
        CreateDepartmentCommand command = Command;
        command.ResponsibleUserId = responsibleUser.Id;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Department department = await context.Departments.SingleAsync(d => d.Id == result.Value);
        department.ResponsibleUserId.ShouldBe(responsibleUser.Id);
    }
}
