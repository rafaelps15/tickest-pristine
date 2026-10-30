using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Application.TicketHistories.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.TicketHistories;

public sealed class GetTicketHistoryQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid CreatorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketHistoryQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketHistoryQuery(Guid.NewGuid(), new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(query.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithHistoryAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetTicketHistoryQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketHistoryQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnNewestHistoryFirst_WhenCallerIsParticipant()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithHistoryAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketHistoryQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketHistoryQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.Items[0].Action.ShouldBe(TicketHistoryAction.MessageAdded);
        result.Value.Items[0].Description.ShouldBe("Nova mensagem adicionada ao chamado");
        result.Value.Items[1].Action.ShouldBe(TicketHistoryAction.Created);
        result.Value.Items[1].Description.ShouldBe("Chamado criado");
    }

    [Fact]
    public async Task Handle_Should_ReturnHistory_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithHistoryAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GetTicketHistoryQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketHistoryQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_DescribeAssigneesByName_IncludingDeactivatedAndRemovedUsers()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithHistoryAsync(context);

        User ana = CreateUser(isActive: false);
        ana.FirstName = "Ana";
        ana.LastName = "Souza";
        User beto = CreateUser();
        beto.FirstName = "Beto";
        beto.LastName = "Lima";
        context.Users.AddRange(ana, beto);

        var removedUser = new User { Id = Guid.NewGuid() };
        DateTime occurredAtUtc = DateTime.UtcNow.AddMinutes(5);
        Ticket ticket = (await context.Tickets.FindAsync(ticketId))!;
        TicketHistory assigned = ticket.Assign(ana, CreatorId, occurredAtUtc);
        TicketHistory reassigned = ticket.Assign(beto, CreatorId, occurredAtUtc.AddMinutes(1));

        // O usuário removido só passa a ser o responsável para a remoção registrá-lo; essa troca não entra no histórico.
        ticket.Assign(removedUser, CreatorId, occurredAtUtc.AddMinutes(2));
        TicketHistory unassigned = ticket.Unassign(CreatorId, occurredAtUtc.AddMinutes(2));
        ticket.ClearDomainEvents();

        context.TicketHistories.AddRange(assigned, reassigned, unassigned);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        var handler = new GetTicketHistoryQueryHandler(context, userContext, Substitute.For<IPermissionProvider>());
        var query = new GetTicketHistoryQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Take(3).Select(h => h.Description).ShouldBe(
        [
            "Responsável removido: usuário removido",
            "Responsável alterado de Ana Souza para Beto Lima",
            "Responsável definido: Ana Souza"
        ]);
    }

    private static async Task<Guid> SeedTicketWithHistoryAsync(TestDbContext context)
    {
        Ticket ticket = TicketTestData.Create(requesterId: CreatorId, description: "Original description");
        context.Tickets.Add(ticket);

        var created = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ChangedByUserId = CreatorId,
            Action = TicketHistoryAction.Created,
            OldValue = null,
            NewValue = null,
            OccurredAtUtc = DateTime.UtcNow
        };
        var messageAdded = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ChangedByUserId = CreatorId,
            Action = TicketHistoryAction.MessageAdded,
            OldValue = null,
            NewValue = null,
            OccurredAtUtc = DateTime.UtcNow.AddMinutes(1)
        };
        context.TicketHistories.AddRange(created, messageAdded);

        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
