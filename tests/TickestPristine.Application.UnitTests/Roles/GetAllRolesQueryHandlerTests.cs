using TickestPristine.Application.Roles.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class GetAllRolesQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnRolesWithTheirPermissionCodes_OrderedByName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var manager = Role.Create("Manager");
        var agent = Role.Create("Agent");
        context.Roles.AddRange(manager, agent);
        context.RolePermissions.Add(RolePermission.Create(manager.Id, "tickets:manage"));
        await context.SaveChangesAsync();

        var handler = new GetAllRolesQueryHandler(context);

        // Act
        Result<List<RoleResponse>> result = await handler.Handle(new GetAllRolesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(r => r.Name).ShouldBe(["Agent", "Manager"]);

        RoleResponse managerResponse = result.Value.Single(r => r.Id == manager.Id);
        managerResponse.PermissionCodes.ShouldContain("tickets:manage");

        RoleResponse agentResponse = result.Value.Single(r => r.Id == agent.Id);
        agentResponse.PermissionCodes.ShouldBeEmpty();
    }
}
