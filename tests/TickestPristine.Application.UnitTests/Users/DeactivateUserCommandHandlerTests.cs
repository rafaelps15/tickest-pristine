using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Users.Deactivate;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class DeactivateUserCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new DeactivateUserCommandHandler(context, CallerContext(), permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.UserId));
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyDeactivated_WhenUserIsAlreadyDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: false);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new DeactivateUserCommandHandler(context, CallerContext(), permissionProvider, Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.AlreadyDeactivated);
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_DeactivateUserAndInvalidatePermissions_WhenUserIsActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var deactivatedAtUtc = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(deactivatedAtUtc);
        var handler = new DeactivateUserCommandHandler(context, CallerContext(), permissionProvider, dateTimeProvider);

        using var requestCancellation = new CancellationTokenSource();

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), requestCancellation.Token);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == userId);
        user.IsActive.ShouldBeFalse();
        user.DeactivatedAtUtc.ShouldBe(deactivatedAtUtc);
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserDeactivatedDomainEvent);

        await permissionProvider.Received(1).InvalidateAsync(userId, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Should_RevokeOnlyUserRefreshTokens_WhenUserIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        var otherUserId = Guid.NewGuid();
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), TokenHash = "hash-1", UserId = userId, ExpiresOnUtc = DateTime.UtcNow.AddDays(7) });
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), TokenHash = "hash-2", UserId = userId, ExpiresOnUtc = DateTime.UtcNow.AddDays(7) });
        context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), TokenHash = "other-hash", UserId = otherUserId, ExpiresOnUtc = DateTime.UtcNow.AddDays(7) });
        await context.SaveChangesAsync();

        var handler = new DeactivateUserCommandHandler(context, CallerContext(), Substitute.For<IPermissionProvider>(), Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<RefreshToken> remaining = await context.RefreshTokens.ToListAsync();
        remaining.ShouldHaveSingleItem().UserId.ShouldBe(otherUserId);
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotDeactivateSelf_WhenCallerDeactivatesOwnAccount()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(userId),
            permissionProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.CannotDeactivateSelf);
        (await context.Users.SingleAsync(u => u.Id == userId)).IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Handle_Should_ReturnLastAdministrator_WhenNoOtherActiveAdministratorExists(bool? otherAdministratorIsActive)
    {
        // Arrange: o outro administrador não existe (null) ou está desativado (false)
        await using TestDbContext context = CreateDbContext();
        Role administratorRole = await SeedAdministratorRoleAsync(context);
        Guid administratorId = await SeedUserAsync(context, isActive: true, administratorRole);

        if (otherAdministratorIsActive is { } isActive)
        {
            await SeedUserAsync(context, isActive, administratorRole);
        }

        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new DeactivateUserCommandHandler(context, CallerContext(), permissionProvider, Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(administratorId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
        context.Entry(await context.Roles.SingleAsync(r => r.Name == administratorRole.Name)).Property<int>("Version").CurrentValue.ShouldBe(0);
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_DeactivateAdministratorAndIncrementVersion_WhenAnotherActiveAdministratorExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Role administratorRole = await SeedAdministratorRoleAsync(context);
        Guid administratorId = await SeedUserAsync(context, isActive: true, administratorRole);
        await SeedUserAsync(context, isActive: true, administratorRole);

        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(),
            Substitute.For<IPermissionProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(administratorId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == administratorId)).IsActive.ShouldBeFalse();
        context.Entry(await context.Roles.SingleAsync(r => r.Name == administratorRole.Name)).Property<int>("Version").CurrentValue.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotManagePrivilegedUser_WhenUserHasAdministrativePermissionCallerLacks()
    {
        // Arrange: quem chama só pode desativar; o alvo também gerencia funções
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        await GrantPermissionAsync(context, userId, PermissionCodes.Roles.Manage);

        var callerId = Guid.NewGuid();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.Deactivate]);
        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(callerId),
            permissionProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.CannotManagePrivilegedUser([PermissionCatalog.GetDisplayName(PermissionCodes.Roles.Manage)]));
        (await context.Users.SingleAsync(u => u.Id == userId)).IsActive.ShouldBeTrue();
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_DeactivateUser_WhenCallerHasAllAdministrativePermissionsOfUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        await GrantPermissionAsync(context, userId, PermissionCodes.Users.Deactivate);

        var callerId = Guid.NewGuid();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.Deactivate]);
        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(callerId),
            permissionProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == userId)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_RevokeUsableInvitations_WhenUserIsPendingActivation()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        UserInvitation invitation = await ResendUserInvitationCommandHandlerTests.SeedInvitationAsync(
            context, userId, "token-do-convite", DefaultUtcNow.AddHours(-1));

        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(),
            Substitute.For<IPermissionProvider>(),
            CreateClock());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserInvitations.SingleAsync(i => i.Id == invitation.Id)).RevokedAtUtc.ShouldBe(DefaultUtcNow);
    }

    [Fact]
    public async Task Handle_Should_ReturnOpenTicketsToQueue_WhenUserHasOpenTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        var callerId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        Ticket open = AddTicket(context, userId, TicketStatus.Open);
        Ticket inProgress = AddTicket(context, userId, TicketStatus.InProgress);
        Ticket resolved = AddTicket(context, userId, TicketStatus.Resolved);
        Ticket closed = AddTicket(context, userId, TicketStatus.Closed);
        Ticket canceled = AddTicket(context, userId, TicketStatus.Canceled);
        Ticket deleted = AddTicket(context, userId, TicketStatus.Open);
        deleted.DeletedAtUtc = DefaultUtcNow;
        Ticket otherAgentTicket = AddTicket(context, otherAgentId, TicketStatus.InProgress);
        await context.SaveChangesAsync();

        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(callerId),
            Substitute.For<IPermissionProvider>(),
            CreateClock());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Dictionary<Guid, (Guid? AssignedToUserId, TicketStatus Status)> saved = await context.Tickets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => (t.AssignedToUserId, t.Status));
        saved[open.Id].ShouldBe((null, TicketStatus.Open));
        saved[inProgress.Id].ShouldBe((null, TicketStatus.Open));
        saved[resolved.Id].ShouldBe((userId, TicketStatus.Resolved));
        saved[closed.Id].ShouldBe((userId, TicketStatus.Closed));
        saved[canceled.Id].ShouldBe((userId, TicketStatus.Canceled));
        saved[deleted.Id].ShouldBe((userId, TicketStatus.Open));
        saved[otherAgentTicket.Id].ShouldBe((otherAgentId, TicketStatus.InProgress));

        open.DomainEvents.OfType<TicketUnassignedDomainEvent>().ShouldHaveSingleItem()
            .ShouldBe(new TicketUnassignedDomainEvent(open.Id, userId, callerId));
        open.DomainEvents.ShouldNotContain(e => e is TicketStatusChangedDomainEvent);
        inProgress.DomainEvents.OfType<TicketUnassignedDomainEvent>().ShouldHaveSingleItem();
        inProgress.DomainEvents.OfType<TicketStatusChangedDomainEvent>().ShouldHaveSingleItem()
            .ShouldBe(new TicketStatusChangedDomainEvent(inProgress.Id, TicketStatus.InProgress, TicketStatus.Open, callerId));
        resolved.DomainEvents.ShouldBeEmpty();
        closed.DomainEvents.ShouldBeEmpty();
        canceled.DomainEvents.ShouldBeEmpty();

        List<TicketHistory> histories = await context.TicketHistories.ToListAsync();
        histories.Count.ShouldBe(3);
        histories.ShouldAllBe(h => h.ChangedByUserId == callerId && h.OccurredAtUtc == DefaultUtcNow);
        histories.Where(h => h.Action == TicketHistoryAction.UnassignedOnUserDeactivation)
            .Select(h => (h.TicketId, h.OldValue, h.NewValue))
            .ShouldBe([(open.Id, userId.ToString(), null), (inProgress.Id, userId.ToString(), null)], ignoreOrder: true);
        TicketHistory statusChanged = histories.Single(h => h.Action == TicketHistoryAction.StatusChanged);
        statusChanged.TicketId.ShouldBe(inProgress.Id);
        statusChanged.OldValue.ShouldBe(nameof(TicketStatus.InProgress));
        statusChanged.NewValue.ShouldBe(nameof(TicketStatus.Open));
    }

    [Fact]
    public async Task Handle_Should_ChangeNoTicket_WhenUserHasNoOpenTickets()
    {
        // Arrange: o usuário só tem chamados encerrados; outro atendente tem um chamado Em Andamento
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        var otherAgentId = Guid.NewGuid();
        Ticket resolved = AddTicket(context, userId, TicketStatus.Resolved);
        Ticket closed = AddTicket(context, userId, TicketStatus.Closed);
        Ticket canceled = AddTicket(context, userId, TicketStatus.Canceled);
        Ticket otherAgentTicket = AddTicket(context, otherAgentId, TicketStatus.InProgress);
        await context.SaveChangesAsync();

        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(),
            Substitute.For<IPermissionProvider>(),
            CreateClock());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == userId)).IsActive.ShouldBeFalse();

        Dictionary<Guid, (Guid? AssignedToUserId, TicketStatus Status)> saved = await context.Tickets
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => (t.AssignedToUserId, t.Status));
        saved[resolved.Id].ShouldBe((userId, TicketStatus.Resolved));
        saved[closed.Id].ShouldBe((userId, TicketStatus.Closed));
        saved[canceled.Id].ShouldBe((userId, TicketStatus.Canceled));
        saved[otherAgentTicket.Id].ShouldBe((otherAgentId, TicketStatus.InProgress));
        new[] { resolved, closed, canceled, otherAgentTicket }.ShouldAllBe(t => t.DomainEvents.Count == 0);
        (await context.TicketHistories.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_SaveUserAndTicketsInSingleSaveChanges_WhenUserHasOpenTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        AddTicket(context, userId, TicketStatus.InProgress);
        await context.SaveChangesAsync();

        int saveCount = 0;
        context.SavedChanges += (_, _) => saveCount++;

        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(),
            Substitute.For<IPermissionProvider>(),
            CreateClock());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        saveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_KeepTicketsAssigned_WhenDeactivationIsRefused()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Role administratorRole = await SeedAdministratorRoleAsync(context);
        Guid administratorId = await SeedUserAsync(context, isActive: true, administratorRole);
        Ticket ticket = AddTicket(context, administratorId, TicketStatus.InProgress);
        await context.SaveChangesAsync();

        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(),
            Substitute.For<IPermissionProvider>(),
            CreateClock());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(administratorId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
        ticket.AssignedToUserId.ShouldBe(administratorId);
        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.DomainEvents.ShouldBeEmpty();
        (await context.TicketHistories.AnyAsync()).ShouldBeFalse();
    }

    private static Ticket AddTicket(TestDbContext context, Guid assignedToUserId, TicketStatus status)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = status,
            CreatedByUserId = Guid.NewGuid(),
            AssignedToUserId = assignedToUserId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DefaultUtcNow
        };

        context.Tickets.Add(ticket);

        return ticket;
    }

    private static async Task GrantPermissionAsync(TestDbContext context, Guid userId, string permissionCode)
    {
        string roleName = $"Role-{Guid.NewGuid():N}";
        context.Roles.Add(CreateRole(roleName));
        context.RolePermissions.Add(new RolePermission { RoleName = roleName, PermissionCode = permissionCode });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleName = roleName });
        await context.SaveChangesAsync();
    }

    private static IUserContext CallerContext(Guid? callerId = null)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId ?? Guid.NewGuid());

        return userContext;
    }

    private static async Task<Role> SeedAdministratorRoleAsync(TestDbContext context)
    {
        Role role = CreateRole(RoleNames.Administrator);
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        return role;
    }

    private static async Task<Guid> SeedUserAsync(TestDbContext context, bool isActive)
    {
        User user = CreateUser(isActive);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    private static async Task<Guid> SeedUserAsync(TestDbContext context, bool isActive, Role role)
    {
        Guid userId = await SeedUserAsync(context, isActive);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleName = role.Name });
        await context.SaveChangesAsync();

        return userId;
    }
}
