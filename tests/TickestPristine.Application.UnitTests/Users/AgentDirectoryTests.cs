using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Domain.Permissions;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class AgentDirectoryTests : BaseHandlerTest
{
    [Fact]
    public async Task IsAgentAsync_Should_BeTrue_WhenUserIsActiveWithManagePermission()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        IPermissionProvider permissionProvider = CreatePermissionProvider();
        permissionProvider.GetCurrentForUserIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Tickets.Manage]);

        // Act
        bool isAgent = await new AgentDirectory(permissionProvider).IsAgentAsync(agentId, CancellationToken.None);

        // Assert
        isAgent.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAgentAsync_Should_BeFalse_WhenUserLacksManagePermissionOrIsInactive()
    {
        // Arrange: a leitura sem cache do provedor devolve vazio para o usuário inativo
        var collaboratorId = Guid.NewGuid();
        var inactiveAgentId = Guid.NewGuid();
        IPermissionProvider permissionProvider = CreatePermissionProvider();
        permissionProvider.GetCurrentForUserIdAsync(collaboratorId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Tickets.UpdateOwn]);
        var agentDirectory = new AgentDirectory(permissionProvider);

        // Act
        bool collaboratorIsAgent = await agentDirectory.IsAgentAsync(collaboratorId, CancellationToken.None);
        bool inactiveIsAgent = await agentDirectory.IsAgentAsync(inactiveAgentId, CancellationToken.None);

        // Assert
        collaboratorIsAgent.ShouldBeFalse();
        inactiveIsAgent.ShouldBeFalse();
    }
}

