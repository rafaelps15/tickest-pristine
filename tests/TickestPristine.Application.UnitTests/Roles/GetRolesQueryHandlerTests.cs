using TickestPristine.Application.Roles.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;

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
        context.RolePermissions.Add(new RolePermission { RoleName = manager.Name, PermissionCode = "tickets:manage" });
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

        Role administrator = CreateRole(RoleNames.Administrator);
        Role collaborator = CreateRole(RoleNames.Collaborator);
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
}
