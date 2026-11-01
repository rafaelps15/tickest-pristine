using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketAgentLookupTests : BaseHandlerTest
{
    [Fact]
    public async Task IsAgentAsync_Should_BeTrue_WhenUserIsActiveWithManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User agent = await SeedUserAsync(context, PermissionCodes.Tickets.Manage);

        // Act
        bool isAgent = await new TicketAgentLookup(context).IsAgentAsync(agent.Id, CancellationToken.None);

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
        var lookup = new TicketAgentLookup(context);

        // Act
        bool collaboratorIsAgent = await lookup.IsAgentAsync(collaborator.Id, CancellationToken.None);
        bool inactiveIsAgent = await lookup.IsAgentAsync(inactiveAgent.Id, CancellationToken.None);

        // Assert
        collaboratorIsAgent.ShouldBeFalse();
        inactiveIsAgent.ShouldBeFalse();
    }

    [Fact]
    public async Task IsLastMessageFromAgentAsync_Should_FollowLastValidMessage_WhenMessagesExist()
    {
        // Arrange: a última mensagem, de quem atende, foi excluída; a anterior é do solicitante.
        await using TestDbContext context = CreateDbContext();
        User agent = await SeedUserAsync(context, PermissionCodes.Tickets.Manage);
        var requesterId = Guid.NewGuid();
        Ticket ticket = TicketTestData.Create(requesterId: requesterId);
        context.Tickets.Add(ticket);
        context.TicketMessages.AddRange(
            TicketTestData.CreateMessage(ticket.Id, agent.Id, "Primeira resposta", DefaultUtcNow),
            TicketTestData.CreateMessage(ticket.Id, requesterId, "Obrigado", DefaultUtcNow.AddMinutes(1)),
            TicketTestData.CreateMessage(ticket.Id, agent.Id, "Excluída", DefaultUtcNow.AddMinutes(2), deletedAtUtc: DefaultUtcNow.AddMinutes(3)));
        await context.SaveChangesAsync();

        // Act
        bool fromAgent = await new TicketAgentLookup(context).IsLastMessageFromAgentAsync(ticket, CancellationToken.None);

        // Assert
        fromAgent.ShouldBeFalse();
    }

    [Fact]
    public async Task IsLastMessageFromAgentAsync_Should_BeTrue_WhenLastValidMessageIsFromAgent()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User agent = await SeedUserAsync(context, PermissionCodes.Tickets.Manage);
        var requesterId = Guid.NewGuid();
        Ticket ticket = TicketTestData.Create(requesterId: requesterId);
        context.Tickets.Add(ticket);
        context.TicketMessages.AddRange(
            TicketTestData.CreateMessage(ticket.Id, requesterId, "Não liga", DefaultUtcNow),
            TicketTestData.CreateMessage(ticket.Id, agent.Id, "Verificando", DefaultUtcNow.AddMinutes(1)));
        await context.SaveChangesAsync();

        // Act
        bool fromAgent = await new TicketAgentLookup(context).IsLastMessageFromAgentAsync(ticket, CancellationToken.None);

        // Assert
        fromAgent.ShouldBeTrue();
    }

    [Fact]
    public async Task IsLastMessageFromAgentAsync_Should_BeFalse_WhenTicketHasNoMessagesOrRequesterIsAgent()
    {
        // Arrange: o solicitante atende chamados, mas a mensagem dele não conta como resposta.
        await using TestDbContext context = CreateDbContext();
        User agentRequester = await SeedUserAsync(context, PermissionCodes.Tickets.Manage);
        Ticket withoutMessages = TicketTestData.Create();
        Ticket ownMessage = TicketTestData.Create(requesterId: agentRequester.Id);
        context.Tickets.AddRange(withoutMessages, ownMessage);
        context.TicketMessages.Add(TicketTestData.CreateMessage(ownMessage.Id, agentRequester.Id, "Abri para mim", DefaultUtcNow));
        await context.SaveChangesAsync();
        var lookup = new TicketAgentLookup(context);

        // Act
        bool withoutMessagesFromAgent = await lookup.IsLastMessageFromAgentAsync(withoutMessages, CancellationToken.None);
        bool ownMessageFromAgent = await lookup.IsLastMessageFromAgentAsync(ownMessage, CancellationToken.None);

        // Assert
        withoutMessagesFromAgent.ShouldBeFalse();
        ownMessageFromAgent.ShouldBeFalse();
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
