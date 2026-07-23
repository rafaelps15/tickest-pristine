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
        result.Error.Code.ShouldBe("Users.NotFound");
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
    }

    [Fact]
    public async Task Handle_Should_CreateDepartment_WhenResponsibleUserExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var responsibleUser = User.Create("responsible@tickestpristine.dev", "Resp", "User");
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
