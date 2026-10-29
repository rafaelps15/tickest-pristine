using TickestPristine.Application.Roles.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class GetRolesQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnRolesWithPermissionCodesOrderedByName_WhenRolesExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        Role manager = CreateRole("Manager");
        Role agent = CreateRole("Atendente");
        context.Roles.AddRange(manager, agent);
        context.RolePermissions.Add(new RolePermission { RoleId = manager.Id, PermissionCode = "tickets:manage" });
        await context.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(context);

        // Act
        Result<List<RoleResponse>> result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(r => r.Name).ShouldBe(["Atendente", "Manager"]);

        RoleResponse managerResponse = result.Value.Single(r => r.Name == manager.Name);
        managerResponse.PermissionCodes.ShouldContain("tickets:manage");

        RoleResponse agentResponse = result.Value.Single(r => r.Name == agent.Name);
        agentResponse.PermissionCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnDefaultAndAdministratorFlags_WhenRolesAreMarked()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        Role administrator = CreateSystemRole(RoleNames.Administrator);
        Role collaborator = CreateSystemRole(RoleNames.Collaborator);
        Role agent = CreateRole("Atendente");
        context.Roles.AddRange(administrator, collaborator, agent);
        await context.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(context);

        // Act
        Result<List<RoleResponse>> result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        RoleResponse administratorResponse = result.Value.Single(r => r.Name == administrator.Name);
        administratorResponse.IsAdministrator.ShouldBeTrue();
        administratorResponse.IsDefault.ShouldBeFalse();

        RoleResponse collaboratorResponse = result.Value.Single(r => r.Name == collaborator.Name);
        collaboratorResponse.IsDefault.ShouldBeTrue();
        collaboratorResponse.IsAdministrator.ShouldBeFalse();

        RoleResponse agentResponse = result.Value.Single(r => r.Name == agent.Name);
        agentResponse.IsDefault.ShouldBeFalse();
        agentResponse.IsAdministrator.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnIdSystemFlagAndUserCounts_WhenRolesHaveActiveAndInactiveUsers()
    {
        // Arrange: uma role fixa e uma criada à mão, com dois usuários ativos e um inativo nela
        await using TestDbContext context = CreateDbContext();

        Role agent = CreateSystemRole(RoleNames.Agent);
        Role supervisor = CreateRole("Supervisor");
        User firstActive = CreateUser();
        User secondActive = CreateUser();
        User inactive = CreateUser(isActive: false);
        context.Roles.AddRange(agent, supervisor);
        context.Users.AddRange(firstActive, secondActive, inactive);
        context.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), UserId = firstActive.Id, RoleId = supervisor.Id },
            new UserRole { Id = Guid.NewGuid(), UserId = secondActive.Id, RoleId = supervisor.Id },
            new UserRole { Id = Guid.NewGuid(), UserId = inactive.Id, RoleId = supervisor.Id },
            new UserRole { Id = Guid.NewGuid(), UserId = firstActive.Id, RoleId = agent.Id });
        await context.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(context);

        // Act
        Result<List<RoleResponse>> result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        RoleResponse supervisorResponse = result.Value.Single(r => r.Name == supervisor.Name);
        supervisorResponse.Id.ShouldBe(supervisor.Id);
        supervisorResponse.IsSystem.ShouldBeFalse();
        supervisorResponse.ActiveUserCount.ShouldBe(2);
        supervisorResponse.UserCount.ShouldBe(3);

        RoleResponse agentResponse = result.Value.Single(r => r.Name == agent.Name);
        agentResponse.Id.ShouldBe(RoleIds.Agent);
        agentResponse.IsSystem.ShouldBeTrue();
        agentResponse.ActiveUserCount.ShouldBe(1);
        agentResponse.UserCount.ShouldBe(1);
    }
}
