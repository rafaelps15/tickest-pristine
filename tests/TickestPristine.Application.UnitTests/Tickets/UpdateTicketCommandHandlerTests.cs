using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets.Update;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class UpdateTicketCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    /// <summary>
    /// Abertura dos chamados dos testes de cancelamento: 27/10/2026, 9h00 em São Paulo (12h00 UTC).
    /// </summary>
    private static readonly DateTime NineAmUtc = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(command.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenNonOwnerLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenOwnerLacksUpdateOwnPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.InProgress };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
        (await context.TicketHistories.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotActive_WhenTicketIsResolved()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved);
        UpdateTicketCommandHandler handler = CreateManagerHandler(context, DateTime.UtcNow);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Closed };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotActive(ticketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenNonOwnerLacksManagePermissionAndTicketIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Closed };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidStatusTransition_WhenTransitionIsNotAllowed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Closed };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.InvalidStatusTransition(TicketStatus.Open, TicketStatus.Closed));
        result.Error.Description.ShouldContain("'Aberto' para 'Fechado'");
    }

    [Fact]
    public async Task Handle_Should_UpdateDescription_WhenOwnerHasUpdateOwnPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Description.ShouldBe("Updated description");
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketUpdatedDomainEvent);
        (await context.TicketHistories.SingleAsync(h => h.TicketId == ticket.Id)).Action
            .ShouldBe(TicketHistoryAction.DescriptionChanged);
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    public async Task Handle_Should_ReturnStatusChangeForbiddenAndChangeNothing_WhenOwnerMovesStatusForward(
        TicketStatus currentStatus,
        TicketStatus newStatus)
    {
        // Arrange: o dono só edita a descrição e cancela; as outras trocas são de quem atende a fila
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, currentStatus);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = newStatus };

        // Act
        Result result = await CreateOwnerHandler(context).Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.StatusChangeForbidden);

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Status.ShouldBe(currentStatus);
        ticket.Description.ShouldBe("Original description");
        (await context.TicketHistories.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_CancelTicket_WhenOwnerIsWithinCancelWindow()
    {
        // Arrange: aberto às 9h00, cancelado pelo dono às 9h14
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NineAmUtc);

        // Act
        Result result = await CreateOwnerHandler(context, NineAmUtc.AddMinutes(14))
            .Handle(CancelCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(TicketStatus.Canceled);
        (await context.TicketHistories.SingleAsync(h => h.TicketId == ticketId)).Action.ShouldBe(TicketHistoryAction.StatusChanged);
    }

    [Fact]
    public async Task Handle_Should_CancelTicket_WhenOwnerCancelsExactlyAtDeadline()
    {
        // Arrange: o prazo do dono é inclusivo (9h15)
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NineAmUtc);

        // Act
        Result result = await CreateOwnerHandler(context, NineAmUtc.AddMinutes(TicketCancellationPolicy.OwnerWindowMinutes))
            .Handle(CancelCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    public async Task Handle_Should_ReturnCancelWindowExpiredAndChangeNothing_WhenOwnerIsPastCancelWindow(TicketStatus status)
    {
        // Arrange: aberto às 9h00, dono tenta cancelar às 9h16
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, status, NineAmUtc);

        // Act
        Result result = await CreateOwnerHandler(context, NineAmUtc.AddMinutes(16))
            .Handle(CancelCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.CancelWindowExpired(TicketCancellationPolicy.OwnerWindowMinutes));
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(status);
        (await context.TicketHistories.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnCancelWindowExpired_WhenOwnerAlsoManagesTicketsButIsPastWindow()
    {
        // Arrange: o dono atende a fila, mas não tem tickets:cancel; fora da janela, vale o erro do prazo do dono
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NineAmUtc);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, CreateClock(NineAmUtc.AddMinutes(16)));

        // Act
        Result result = await handler.Handle(CancelCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.CancelWindowExpired(TicketCancellationPolicy.OwnerWindowMinutes));
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(TicketStatus.Open);
    }

    [Fact]
    public async Task Handle_Should_CancelTicket_WhenOwnerWithoutUpdateOwnHasCancelPermission()
    {
        // Arrange: o dono não tem tickets:update-own, mas tem tickets:cancel, que vale a qualquer momento
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NineAmUtc);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.Cancel, Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, CreateClock(NineAmUtc.AddHours(2)));

        // Act
        Result result = await handler.Handle(CancelCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(TicketStatus.Canceled);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerWithOnlyCancelPermissionMovesStatusForward()
    {
        // Arrange: tickets:cancel só serve para cancelar
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NineAmUtc);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Original description", Status = TicketStatus.InProgress };

        // Act
        Result result = await CreateCancelerHandler(context, NineAmUtc.AddHours(2)).Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(TicketStatus.Open);
    }

    [Fact]
    public async Task Handle_Should_ReturnCancelForbiddenAndChangeNothing_WhenManagerLacksCancelPermission()
    {
        // Arrange: chamado aberto há 2 horas; quem atende a fila não tem tickets:cancel
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NineAmUtc);

        // Act
        Result result = await CreateManagerHandler(context, NineAmUtc.AddHours(2))
            .Handle(CancelCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.CancelForbidden);
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(TicketStatus.Open);
    }

    [Fact]
    public async Task Handle_Should_CancelTicket_WhenCallerHasCancelPermission()
    {
        // Arrange: quem só tem tickets:cancel, sem ser o dono nem atender a fila, cancela depois de 2 horas
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NineAmUtc);

        // Act
        Result result = await CreateCancelerHandler(context, NineAmUtc.AddHours(2))
            .Handle(CancelCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(TicketStatus.Canceled);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerWithOnlyCancelPermissionChangesDescription()
    {
        // Arrange: tickets:cancel não dá direito a editar a descrição
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NineAmUtc);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Outra descrição", Status = TicketStatus.Canceled };

        // Act
        Result result = await CreateCancelerHandler(context, NineAmUtc.AddHours(2)).Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(TicketStatus.Open);
    }

    [Fact]
    public async Task Handle_Should_NotRaiseStatusChangedEvent_WhenStatusIsUnchanged()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketUpdatedDomainEvent);
        ticket.DomainEvents.ShouldNotContain(domainEvent => domainEvent is TicketStatusChangedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_RecordResolutionTime_WhenTicketIsResolvedForTheFirstTime()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress);
        var resolvedAtUtc = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        UpdateTicketCommandHandler handler = CreateManagerHandler(context, resolvedAtUtc);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Fixed", Status = TicketStatus.Resolved };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.ResolvedAtUtc.ShouldBe(resolvedAtUtc);
    }

    [Fact]
    public async Task Handle_Should_KeepFirstResolutionTime_WhenReopenedTicketIsResolvedAgain()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress);
        var firstResolutionUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        Ticket seeded = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        seeded.ResolvedAtUtc = firstResolutionUtc;
        await context.SaveChangesAsync();

        UpdateTicketCommandHandler handler = CreateManagerHandler(context, firstResolutionUtc.AddDays(4));
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Fixed again", Status = TicketStatus.Resolved };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.ResolvedAtUtc.ShouldBe(firstResolutionUtc);
    }

    [Fact]
    public async Task Handle_Should_RecordDescriptionChanged_WhenOnlyDescriptionChanges()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);
        UpdateTicketCommandHandler handler = CreateOwnerHandler(context);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticketId);
        history.Action.ShouldBe(TicketHistoryAction.DescriptionChanged);
        history.OldValue.ShouldBe("Original description");
        history.NewValue.ShouldBe("Updated description");
        history.ChangedByUserId.ShouldBe(OwnerId);
    }

    [Fact]
    public async Task Handle_Should_RecordStatusAndDescriptionChanges_WhenManagerChangesBoth()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);
        UpdateTicketCommandHandler handler = CreateManagerHandler(context, DateTime.UtcNow);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.InProgress };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        List<TicketHistory> histories = await context.TicketHistories.Where(h => h.TicketId == ticketId).ToListAsync();
        histories.Count.ShouldBe(2);

        TicketHistory statusChanged = histories.Single(h => h.Action == TicketHistoryAction.StatusChanged);
        statusChanged.OldValue.ShouldBe(nameof(TicketStatus.Open));
        statusChanged.NewValue.ShouldBe(nameof(TicketStatus.InProgress));

        TicketHistory descriptionChanged = histories.Single(h => h.Action == TicketHistoryAction.DescriptionChanged);
        descriptionChanged.OldValue.ShouldBe("Original description");
        descriptionChanged.NewValue.ShouldBe("Updated description");
    }

    [Fact]
    public async Task Handle_Should_RecordOnlyStatusChanged_WhenDescriptionIsUnchanged()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);
        UpdateTicketCommandHandler handler = CreateManagerHandler(context, DateTime.UtcNow);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Original description", Status = TicketStatus.InProgress };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.TicketHistories.SingleAsync(h => h.TicketId == ticketId)).Action.ShouldBe(TicketHistoryAction.StatusChanged);
    }

    [Fact]
    public async Task Handle_Should_RecordNothing_WhenDescriptionAndStatusAreUnchanged()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);
        UpdateTicketCommandHandler handler = CreateOwnerHandler(context);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Original description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.TicketHistories.AnyAsync(h => h.TicketId == ticketId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_RecordDescriptionChanged_WhenDescriptionDiffersOnlyInCase()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);
        UpdateTicketCommandHandler handler = CreateOwnerHandler(context);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "original description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticketId);
        history.Action.ShouldBe(TicketHistoryAction.DescriptionChanged);
        history.NewValue.ShouldBe("original description");
    }

    private static UpdateTicketCommandHandler CreateOwnerHandler(TestDbContext context)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        return new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
    }

    private static UpdateTicketCommandHandler CreateManagerHandler(TestDbContext context, DateTime nowUtc)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);

        return new UpdateTicketCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
    }

    /// <summary>
    /// Quem tem só <c>tickets:cancel</c>: não é o dono nem atende a fila.
    /// </summary>
    private static UpdateTicketCommandHandler CreateCancelerHandler(TestDbContext context, DateTime nowUtc)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Cancel, Arg.Any<CancellationToken>())
            .Returns(true);

        return new UpdateTicketCommandHandler(context, userContext, permissionProvider, CreateClock(nowUtc));
    }

    private static async Task<Guid> SeedTicketAsync(
        TestDbContext context,
        Guid openedByUserId,
        TicketStatus status,
        DateTime? createdAtUtc = null)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = openedByUserId,
            AssignedToUserId = null,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };

        if (status != TicketStatus.Open)
        {
            ticket.Status = status;
        }

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }

    // Abertura dos chamados dos testes de SLA: segunda-feira, 26/10/2026, 08:10 em São Paulo.
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);

    private static UpdateTicketCommandHandler CreateOwnerHandler(TestDbContext context, DateTime nowUtc)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        return new UpdateTicketCommandHandler(context, userContext, permissionProvider, CreateClock(nowUtc));
    }

    /// <summary>
    /// Cancela o chamado sem mudar a descrição gravada por <see cref="SeedTicketAsync"/>.
    /// </summary>
    private static UpdateTicketCommand CancelCommand(Guid ticketId) =>
        new() { TicketId = ticketId, Description = "Original description", Status = TicketStatus.Canceled };

    private static UpdateTicketCommand StatusCommand(Guid ticketId, TicketStatus status) =>
        new() { TicketId = ticketId, Description = "The printer on the 3rd floor is not working", Status = status };

    [Fact]
    public async Task Handle_Should_CompleteResolutionAndFirstResponseTimers_WhenResolvedWithoutResponse()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, OwnerId, OpenedAtUtc, TicketStatus.InProgress);
        // Antes dos dois prazos: primeira resposta às 08:40 e resolução às 12:10.
        DateTime resolvedAtUtc = OpenedAtUtc.AddMinutes(20);

        // Act
        Result result = await CreateManagerHandler(context, resolvedAtUtc).Handle(StatusCommand(ticket.Id, TicketStatus.Resolved), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();
        timers.Count.ShouldBe(2);
        timers.ShouldAllBe(t => t.CompletedOnUtc == resolvedAtUtc && t.StoppedOnUtc == null);
        timers.ShouldAllBe(t => t.CompletedOnUtc <= t.DueOnUtc);
    }

    [Fact]
    public async Task Handle_Should_StopOpenSlaTimers_WhenCanceled()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, OwnerId, OpenedAtUtc);
        // Dentro da janela em que o dono pode cancelar.
        DateTime canceledAtUtc = OpenedAtUtc.AddMinutes(10);

        // Act
        Result result = await CreateOwnerHandler(context, canceledAtUtc).Handle(StatusCommand(ticket.Id, TicketStatus.Canceled), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();
        timers.ShouldAllBe(t => t.StoppedOnUtc == canceledAtUtc && t.CompletedOnUtc == null);

        Ticket saved = await context.Tickets.SingleAsync(t => t.Id == ticket.Id);
        TicketSla.GetStatus(saved.Status, saved.DueAtUtc, saved.SlaWarningAtUtc, saved.ResolvedAtUtc, canceledAtUtc)
            .ShouldBe(TicketSlaStatus.NotApplicable);
    }

    [Fact]
    public async Task Handle_Should_NotTouchSlaTimers_WhenMovedToInProgress()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, OwnerId, OpenedAtUtc);

        // Act
        Result result = await CreateManagerHandler(context, OpenedAtUtc.AddMinutes(5))
            .Handle(StatusCommand(ticket.Id, TicketStatus.InProgress), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();
        timers.ShouldAllBe(t => t.CompletedOnUtc == null && t.StoppedOnUtc == null);
    }
}
