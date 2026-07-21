using TickestPristine.Application.Roles.Create;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class CreateRoleCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenNameIsNotUnique()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Roles.Add(Role.Create("Manager"));
        await context.SaveChangesAsync();

        var handler = new CreateRoleCommandHandler(context);

        // Act
        Result<Guid> result = await handler.Handle(new CreateRoleCommand { Name = "Manager" }, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NameNotUnique);
    }

    [Fact]
    public async Task Handle_Should_CreateRole_WhenNameIsUnique()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new CreateRoleCommandHandler(context);

        // Act
        Result<Guid> result = await handler.Handle(new CreateRoleCommand { Name = "Manager" }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Role role = await context.Roles.SingleAsync(r => r.Id == result.Value);
        role.Name.ShouldBe("Manager");
    }
}
