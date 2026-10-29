using TickestPristine.Application.Roles;
using TickestPristine.Application.Roles.GetById;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class GetRoleByIdQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnRoleInTheListingFormat_WhenRoleExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        Role supervisor = CreateRole("Supervisor");
        User active = CreateUser();
        User inactive = CreateUser(isActive: false);
        context.Roles.AddRange(supervisor, CreateRole("Outra"));
        context.Users.AddRange(active, inactive);
        context.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), UserId = active.Id, RoleId = supervisor.Id },
            new UserRole { Id = Guid.NewGuid(), UserId = inactive.Id, RoleId = supervisor.Id });
        context.RolePermissions.Add(new RolePermission { RoleId = supervisor.Id, PermissionCode = "tickets:manage" });
        await context.SaveChangesAsync();

        var handler = new GetRoleByIdQueryHandler(context);

        // Act
        Result<RoleResponse> result = await handler.Handle(new GetRoleByIdQuery(supervisor.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(supervisor.Id);
        result.Value.Name.ShouldBe("Supervisor");
        result.Value.IsSystem.ShouldBeFalse();
        result.Value.IsDefault.ShouldBeFalse();
        result.Value.IsAdministrator.ShouldBeFalse();
        result.Value.ActiveUserCount.ShouldBe(1);
        result.Value.UserCount.ShouldBe(2);
        result.Value.PermissionCodes.ShouldBe(["tickets:manage"]);
    }

    [Fact]
    public async Task Handle_Should_FlagSystemAndAdministrator_WhenRoleIsTheAdministrator()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Roles.Add(CreateSystemRole(RoleNames.Administrator));
        await context.SaveChangesAsync();

        var handler = new GetRoleByIdQueryHandler(context);

        // Act
        Result<RoleResponse> result = await handler.Handle(new GetRoleByIdQuery(RoleIds.Administrator), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsSystem.ShouldBeTrue();
        result.Value.IsAdministrator.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenRoleDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new GetRoleByIdQueryHandler(context);
        var roleId = Guid.NewGuid();

        // Act
        Result<RoleResponse> result = await handler.Handle(new GetRoleByIdQuery(roleId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound(roleId));
    }
}
