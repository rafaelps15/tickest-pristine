using TickestPristine.Application.Permissions;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class AgentDirectoryTests : BaseHandlerTest
{
    [Fact]
    public async Task IsAgentAsync_Should_BeTrue_WhenUserIsActiveWithManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User agent = await SeedUserAsync(context, PermissionCodes.Tickets.Manage);

        // Act
        bool isAgent = await new AgentDirectory(context).IsAgentAsync(agent.Id, CancellationToken.None);

        // Assert
        isAgent.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAgentAsync_Should_BeFalse_WhenUserLacksManagePermissionOrIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User collaborator = await SeedUserAsync(context, PermissionCodes.Tickets.UpdateOwn);
        User inactiveAgent = await SeedUserAsync(context, PermissionCodes.Tickets.Manage, isActive: false);
        var agentDirectory = new AgentDirectory(context);

        // Act
        bool collaboratorIsAgent = await agentDirectory.IsAgentAsync(collaborator.Id, CancellationToken.None);
        bool inactiveIsAgent = await agentDirectory.IsAgentAsync(inactiveAgent.Id, CancellationToken.None);

        // Assert
        collaboratorIsAgent.ShouldBeFalse();
        inactiveIsAgent.ShouldBeFalse();
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, string permissionCode, bool isActive = true)
    {
        User user = CreateUser(isActive);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await GrantPermissionsAsync(context, user.Id, permissionCode);

        return user;
    }
}
