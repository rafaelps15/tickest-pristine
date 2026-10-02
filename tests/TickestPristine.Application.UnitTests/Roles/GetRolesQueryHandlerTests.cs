using TickestPristine.Application.Roles.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class GetRolesQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnRolesWithPermissionCodesOrderedByName_WhenRolesExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var manager = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        var agent = new Role { Id = Guid.NewGuid(), Name = "Atendente" };
        context.Roles.AddRange(manager, agent);
        context.RolePermissions.Add(new RolePermission { RoleId = manager.Id, PermissionCode = "tickets:manage" });
        await context.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(context);

        // Act
        Result<List<RoleResponse>> result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(r => r.Name).ShouldBe(["Atendente", "Manager"]);

        RoleResponse managerResponse = result.Value.Single(r => r.Id == manager.Id);
        managerResponse.PermissionCodes.ShouldContain("tickets:manage");

        RoleResponse agentResponse = result.Value.Single(r => r.Id == agent.Id);
        agentResponse.PermissionCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnDefaultAndAdministratorFlags_WhenRolesAreMarked()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var administrator = new Role { Id = Guid.NewGuid(), Name = "Administrador do Sistema", IsAdministrator = true };
        var collaborator = new Role { Id = Guid.NewGuid(), Name = "Colaborador", IsDefault = true };
        var agent = new Role { Id = Guid.NewGuid(), Name = "Atendente" };
        context.Roles.AddRange(administrator, collaborator, agent);
        await context.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(context);

        // Act
        Result<List<RoleResponse>> result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        RoleResponse administratorResponse = result.Value.Single(r => r.Id == administrator.Id);
        administratorResponse.IsAdministrator.ShouldBeTrue();
        administratorResponse.IsDefault.ShouldBeFalse();

        RoleResponse collaboratorResponse = result.Value.Single(r => r.Id == collaborator.Id);
        collaboratorResponse.IsDefault.ShouldBeTrue();
        collaboratorResponse.IsAdministrator.ShouldBeFalse();

        RoleResponse agentResponse = result.Value.Single(r => r.Id == agent.Id);
        agentResponse.IsDefault.ShouldBeFalse();
        agentResponse.IsAdministrator.ShouldBeFalse();
    }
}
