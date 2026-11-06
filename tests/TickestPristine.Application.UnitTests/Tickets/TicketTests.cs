using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketTests
{
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OccurredAtUtc = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueAtUtc = OpenedAtUtc.AddHours(4);
    private static readonly DateTime SlaWarningAtUtc = OpenedAtUtc.AddHours(3);
    private static readonly Guid AgentId = Guid.NewGuid();

    private static readonly Sector Helpdesk =
        Sector.Create(Department.Create("Suporte", "Atendimento", responsibleUser: null), "Helpdesk", description: null);

    // Calendário 24 horas: os prazos de 240 minutos caem 4 h depois da abertura, e o alerta, 3 h depois.
    private static readonly TicketSlaPlan Plan = new(null, "Padrão", 1, SlaCalendarIds.Continuous, 30, 240, IsLegacy: false);

    private static readonly BusinessCalendar Calendar = ContinuousSlaCalendar.Create();

    [Fact]
    public void Create_Should_OpenTicketAndRaiseEvent_WhenCalled()
    {
        // Arrange
        var requester = new User { Id = Guid.NewGuid() };
        var assignee = new User { Id = Guid.NewGuid() };
        var openedByUserId = Guid.NewGuid();

        // Act
        var ticket = Ticket.Create(
            "Printer is broken",
            "The printer on the 3rd floor is not working",
            TicketPriority.Urgent,
            Helpdesk,
            requester,
            assignee,
            Plan,
            Calendar,
            openedByUserId,
            OpenedAtUtc);

        // Assert
        ticket.Id.ShouldNotBe(Guid.Empty);
        ticket.Title.ShouldBe("Printer is broken");
        ticket.Description.ShouldBe("The printer on the 3rd floor is not working");
        ticket.Priority.ShouldBe(TicketPriority.Urgent);
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.CreatedByUserId.ShouldBe(requester.Id);
        ticket.AssignedToUserId.ShouldBe(assignee.Id);
        ticket.SectorId.ShouldBe(Helpdesk.Id);
        ticket.CreatedAtUtc.ShouldBe(OpenedAtUtc);
        ticket.LastActivityAtUtc.ShouldBe(ticket.CreatedAtUtc);
        ticket.DueAtUtc.ShouldBe(DueAtUtc);
        ticket.SlaWarningAtUtc.ShouldBe(SlaWarningAtUtc);
        ticket.SlaPlan.ShouldBe(Plan);
        ticket.ResolvedAtUtc.ShouldBeNull();
        ticket.DeletedAtUtc.ShouldBeNull();
        ticket.DomainEvents.OfType<TicketCreatedDomainEvent>()
            .ShouldContain(e => e.TicketId == ticket.Id && e.OpenedByUserId == openedByUserId);
    }

    [Fact]
    public void Create_Should_LeaveTicketUnassigned_WhenAssigneeIsNull()
    {
        // Arrange
        var requester = new User { Id = Guid.NewGuid() };

        // Act
        var ticket = Ticket.Create(
            "Printer is broken",
            "The printer on the 3rd floor is not working",
            TicketPriority.Medium,
            Helpdesk,
            requester,
            assignee: null,
            Plan,
            Calendar,
            requester.Id,
            OpenedAtUtc);

        // Assert
        ticket.AssignedToUserId.ShouldBeNull();
    }

    [Fact]
    public void RecordOpening_Should_RecordOpenStatusByCreator_WhenCalled()
    {
        // Arrange
        Ticket ticket = CreateTicket();
        var openedByUserId = Guid.NewGuid();

        // Act
        TicketHistoryEntry history = ticket.RecordOpening(openedByUserId);

        // Assert
        history.Id.ShouldNotBe(Guid.Empty);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(openedByUserId);
        history.Action.ShouldBe(TicketHistoryAction.Created);
        history.OldValue.ShouldBeNull();
        history.NewValue.ShouldBe(nameof(TicketStatus.Open));
        history.OccurredAtUtc.ShouldBe(OpenedAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.Open, true)]
    [InlineData(TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Canceled, false)]
    public void IsActive_Should_BeTrueOnlyForOpenAndInProgress_WhenTicketIsInStatus(TicketStatus status, bool expected)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(status);

        // Act
        bool isActive = ticket.IsActive;

        // Assert
        isActive.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public void Active_Should_AgreeWithIsActive_WhenTicketIsInStatus(TicketStatus status)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(status);
        Func<Ticket, bool> active = Ticket.Active.Compile();

        // Act
        bool matches = active(ticket);

        // Assert
        matches.ShouldBe(ticket.IsActive);
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.Open, false)]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Open, TicketStatus.Canceled, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Open, false)]
    [InlineData(TicketStatus.InProgress, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Canceled, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Open, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Canceled, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open, true)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Canceled, false)]
    [InlineData(TicketStatus.Canceled, TicketStatus.Open, true)]
    [InlineData(TicketStatus.Canceled, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.Canceled, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Canceled, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Canceled, TicketStatus.Canceled, false)]
    public void CanTransitionTo_Should_AllowOnlyPermittedTransitions_WhenTargetStatusIsGiven(
        TicketStatus current,
        TicketStatus target,
        bool expected)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(current);

        // Act
        bool canTransition = ticket.CanTransitionTo(target);

        // Assert
        canTransition.ShouldBe(expected);
    }

    [Fact]
    public void IsAssignedTo_Should_BeTrueOnlyForCurrentAssignee_WhenTicketIsAssigned()
    {
        // Arrange
        var assignee = new User { Id = Guid.NewGuid() };
        Ticket ticket = CreateTicket(assignee);

        // Act
        bool isAssignedToAssignee = ticket.IsAssignedTo(assignee.Id);
        bool isAssignedToOther = ticket.IsAssignedTo(Guid.NewGuid());

        // Assert
        isAssignedToAssignee.ShouldBeTrue();
        isAssignedToOther.ShouldBeFalse();
    }

    [Fact]
    public void StartProgress_Should_RecordOnlyStatusChange_WhenTicketIsOpen()
    {
        // Arrange
        Ticket ticket = CreateTicket();
        string description = ticket.Description;

        // Act
        TicketHistoryEntry history = ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, OccurredAtUtc).Value;

        // Assert
        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.Description.ShouldBe(description);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.StatusChanged);
        history.OldValue.ShouldBe(nameof(TicketStatus.Open));
        history.NewValue.ShouldBe(nameof(TicketStatus.InProgress));
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketStatusChangedDomainEvent>().ShouldContain(e =>
            e.TicketId == ticket.Id &&
            e.OldStatus == TicketStatus.Open &&
            e.NewStatus == TicketStatus.InProgress &&
            e.ChangedByUserId == AgentId);
    }

    [Theory]
    [InlineData(TicketStatus.InProgress, "Tickets.InvalidStatusTransition")]
    [InlineData(TicketStatus.Resolved, "Tickets.NotActive")]
    [InlineData(TicketStatus.Closed, "Tickets.NotActive")]
    [InlineData(TicketStatus.Canceled, "Tickets.NotActive")]
    public void StartProgress_Should_FailWithoutChange_WhenTransitionIsNotAllowed(TicketStatus status, string expectedErrorCode)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(status);
        DateTime lastActivityAtUtc = ticket.LastActivityAtUtc;

        // Act
        Result<TicketHistoryEntry> result = ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, OccurredAtUtc);

        // Assert
        result.Error.Code.ShouldBe(expectedErrorCode);
        ticket.Status.ShouldBe(status);
        ticket.LastActivityAtUtc.ShouldBe(lastActivityAtUtc);
        ticket.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public void Close_Should_FailWithoutChange_WhenTicketIsNotResolved(TicketStatus status)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(status);
        DateTime lastActivityAtUtc = ticket.LastActivityAtUtc;

        // Act
        Result<TicketHistoryEntry> result = ticket.Close(AgentId, OccurredAtUtc);

        // Assert
        result.Error.ShouldBe(TicketErrors.InvalidStatusTransition(status, TicketStatus.Closed));
        ticket.Status.ShouldBe(status);
        ticket.LastActivityAtUtc.ShouldBe(lastActivityAtUtc);
        ticket.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ChangeDescription_Should_RecordOnlyDescriptionChange_WhenDescriptionChanges()
    {
        // Arrange
        Ticket ticket = CreateTicket(description: "Original description");

        // Act
        TicketHistoryEntry? history = ticket.ChangeDescription("Updated description", AgentId, OccurredAtUtc);

        // Assert
        ticket.Description.ShouldBe("Updated description");
        ticket.Status.ShouldBe(TicketStatus.Open);
        history.ShouldNotBeNull();
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.DescriptionChanged);
        history.OldValue.ShouldBe("Original description");
        history.NewValue.ShouldBe("Updated description");
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new TicketDescriptionChangedDomainEvent(ticket.Id, "Original description"));
    }

    [Fact]
    public void StartProgressAndChangeDescription_Should_RecordStatusBeforeDescription_WhenBothChange()
    {
        // Arrange
        Ticket ticket = CreateTicket(description: "Original description");

        // Act: na ordem do PUT do chamado, o status antes da descrição.
        TicketHistoryEntry statusChange = ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, OccurredAtUtc).Value;
        TicketHistoryEntry descriptionChange = ticket.ChangeDescription("Updated description", AgentId, OccurredAtUtc)!;
        TicketHistoryEntry[] entries = [statusChange, descriptionChange];

        // Assert
        entries.Select(e => e.Action).ShouldBe([TicketHistoryAction.StatusChanged, TicketHistoryAction.DescriptionChanged]);
        entries[0].OldValue.ShouldBe(nameof(TicketStatus.Open));
        entries[0].NewValue.ShouldBe(nameof(TicketStatus.InProgress));
        entries[1].OldValue.ShouldBe("Original description");
        entries[1].NewValue.ShouldBe("Updated description");
        entries.ShouldAllBe(e => e.TicketId == ticket.Id && e.ChangedByUserId == AgentId && e.OccurredAtUtc == OccurredAtUtc);
        ticket.DomainEvents.Select(e => e.GetType()).ShouldBe(
            [typeof(TicketStatusChangedDomainEvent), typeof(TicketDescriptionChangedDomainEvent)]);
    }

    [Fact]
    public void ChangeDescription_Should_ReturnNullWithoutEvent_WhenDescriptionDoesNotChange()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        TicketHistoryEntry? history = ticket.ChangeDescription(ticket.Description, AgentId, OccurredAtUtc);

        // Assert
        history.ShouldBeNull();
        ticket.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_Should_RecordResolutionTime_WhenTicketIsResolvedForTheFirstTime()
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.InProgress);

        // Act
        new TicketSlaCycleService().Resolve(ticket, [], AgentId, OccurredAtUtc);

        // Assert
        ticket.Status.ShouldBe(TicketStatus.Resolved);
        ticket.ResolvedAtUtc.ShouldBe(OccurredAtUtc);
    }

    [Fact]
    public void Resolve_Should_KeepFirstResolutionTime_WhenReopenedTicketIsResolvedAgain()
    {
        // Arrange
        var slaCycle = new TicketSlaCycleService();
        Ticket ticket = CreateTicketIn(TicketStatus.Resolved);
        DateTime firstResolutionUtc = ticket.ResolvedAtUtc!.Value;
        slaCycle.Reopen(ticket, [], Calendar, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc);
        ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, OccurredAtUtc);

        // Act
        slaCycle.Resolve(ticket, [], AgentId, OccurredAtUtc.AddHours(1));

        // Assert
        ticket.ResolvedAtUtc.ShouldBe(firstResolutionUtc);
    }

    [Fact]
    public void Assign_Should_RecordAssignedWithoutOldValue_WhenTicketHasNoAssignee()
    {
        // Arrange
        Ticket ticket = CreateTicket();
        var assignee = new User { Id = Guid.NewGuid() };

        // Act
        TicketHistoryEntry history = ticket.Assign(assignee, AgentId, OccurredAtUtc).Value;

        // Assert
        ticket.AssignedToUserId.ShouldBe(assignee.Id);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.Assigned);
        history.OldValue.ShouldBeNull();
        history.NewValue.ShouldBe(assignee.Id.ToString());
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketAssignedDomainEvent>().ShouldContain(e =>
            e.TicketId == ticket.Id &&
            e.PreviousAssignedToUserId == null &&
            e.AssignedToUserId == assignee.Id &&
            e.ChangedByUserId == AgentId);
    }

    [Fact]
    public void Assign_Should_RecordReassignedWithPreviousAndNew_WhenTicketHasAssignee()
    {
        // Arrange
        var previous = new User { Id = Guid.NewGuid() };
        Ticket ticket = CreateTicket(previous);
        var assignee = new User { Id = Guid.NewGuid() };

        // Act
        TicketHistoryEntry history = ticket.Assign(assignee, AgentId, OccurredAtUtc).Value;

        // Assert
        ticket.AssignedToUserId.ShouldBe(assignee.Id);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.Reassigned);
        history.OldValue.ShouldBe(previous.Id.ToString());
        history.NewValue.ShouldBe(assignee.Id.ToString());
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketAssignedDomainEvent>().ShouldContain(e =>
            e.TicketId == ticket.Id &&
            e.PreviousAssignedToUserId == previous.Id &&
            e.AssignedToUserId == assignee.Id &&
            e.ChangedByUserId == AgentId);
    }

    [Fact]
    public void Unassign_Should_RecordPreviousAssigneeWithoutNewValue_WhenTicketHasAssignee()
    {
        // Arrange
        var previous = new User { Id = Guid.NewGuid() };
        Ticket ticket = CreateTicket(previous);

        // Act
        TicketHistoryEntry history = ticket.Unassign(AgentId, OccurredAtUtc).Value;

        // Assert
        ticket.AssignedToUserId.ShouldBeNull();
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.Unassigned);
        history.OldValue.ShouldBe(previous.Id.ToString());
        history.NewValue.ShouldBeNull();
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketUnassignedDomainEvent>().ShouldContain(e =>
            e.TicketId == ticket.Id &&
            e.PreviousAssignedToUserId == previous.Id &&
            e.ChangedByUserId == AgentId);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public void Assign_Should_FailWithoutChange_WhenTicketIsInactive(TicketStatus status)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(status);
        DateTime lastActivityAtUtc = ticket.LastActivityAtUtc;

        // Act
        Result<TicketHistoryEntry> result = ticket.Assign(new User { Id = Guid.NewGuid() }, AgentId, OccurredAtUtc);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotActive(ticket.Id));
        ticket.AssignedToUserId.ShouldBeNull();
        ticket.LastActivityAtUtc.ShouldBe(lastActivityAtUtc);
        ticket.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public void Unassign_Should_FailWithoutChange_WhenTicketIsInactive(TicketStatus status)
    {
        // Arrange
        var assignee = new User { Id = Guid.NewGuid() };
        Ticket ticket = CreateTicketIn(status, assignee);
        DateTime lastActivityAtUtc = ticket.LastActivityAtUtc;

        // Act
        Result<TicketHistoryEntry> result = ticket.Unassign(AgentId, OccurredAtUtc);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotActive(ticket.Id));
        ticket.AssignedToUserId.ShouldBe(assignee.Id);
        ticket.LastActivityAtUtc.ShouldBe(lastActivityAtUtc);
        ticket.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Close_Should_CloseAndRecordStatusChange_WhenTicketIsResolved()
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.Resolved);

        // Act
        TicketHistoryEntry history = ticket.Close(AgentId, OccurredAtUtc).Value;

        // Assert
        ticket.Status.ShouldBe(TicketStatus.Closed);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.StatusChanged);
        history.OldValue.ShouldBe(nameof(TicketStatus.Resolved));
        history.NewValue.ShouldBe(nameof(TicketStatus.Closed));
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketStatusChangedDomainEvent>().ShouldContain(e =>
            e.TicketId == ticket.Id &&
            e.OldStatus == TicketStatus.Resolved &&
            e.NewStatus == TicketStatus.Closed &&
            e.ChangedByUserId == AgentId);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public void Reopen_Should_OpenAndRecordReopened_WhenTicketIsInactive(TicketStatus status)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(status);
        DateTime? resolvedAtUtc = ticket.ResolvedAtUtc;

        // Act
        TicketHistoryEntry history = new TicketSlaCycleService()
            .Reopen(ticket, [], Calendar, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc).Value.History;

        // Assert
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.ResolvedAtUtc.ShouldBe(resolvedAtUtc);
        ticket.DueAtUtc.ShouldBe(DueAtUtc);
        ticket.SlaWarningAtUtc.ShouldBe(SlaWarningAtUtc);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.Reopened);
        history.OldValue.ShouldBe(status.ToString());
        history.NewValue.ShouldBe(nameof(TicketStatus.Open));
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketReopenedDomainEvent>()
            .ShouldContain(e => e.TicketId == ticket.Id && e.ReopenedByUserId == AgentId);
    }

    [Fact]
    public void Delete_Should_MarkDeletedAndRecordDeleted_WhenCalled()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        TicketHistoryEntry history = new TicketSlaCycleService().Delete(ticket, [], AgentId, OccurredAtUtc).Value;

        // Assert
        ticket.DeletedAtUtc.ShouldBe(OccurredAtUtc);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.Deleted);
        history.OldValue.ShouldBeNull();
        history.NewValue.ShouldBeNull();
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketDeletedDomainEvent>().ShouldContain(e => e.TicketId == ticket.Id);
    }

    [Fact]
    public void Create_Should_WaitOnAgent_WhenTicketIsOpened()
    {
        // Act
        Ticket ticket = CreateTicket();

        // Assert
        ticket.WaitingOn.ShouldBe(TicketWaitingOn.Agent);
    }

    [Fact]
    public void OwnerCancelDeadline_Should_BeFifteenMinutesAfterOpening()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        DateTime deadline = Ticket.OwnerCancelDeadline(ticket.CreatedAtUtc);

        // Assert
        Ticket.OwnerCancelWindowMinutes.ShouldBe(15);
        deadline.ShouldBe(OpenedAtUtc.AddMinutes(15));
    }

    [Theory]
    [InlineData(TicketStatus.Open, 10, false, true, null)]
    [InlineData(TicketStatus.InProgress, 10, false, true, null)]
    [InlineData(TicketStatus.Open, 15, false, true, null)]
    [InlineData(TicketStatus.Open, 16, false, true, "Tickets.CancelWindowExpired")]
    [InlineData(TicketStatus.Open, 20, true, false, null)]
    [InlineData(TicketStatus.Open, 10, false, false, "Tickets.CancelForbidden")]
    [InlineData(TicketStatus.Resolved, 10, true, true, "Tickets.NotActive")]
    [InlineData(TicketStatus.Closed, 10, true, false, "Tickets.NotActive")]
    [InlineData(TicketStatus.Canceled, 10, true, false, "Tickets.NotActive")]
    public void Cancel_Should_RequireActiveTicketAndPermissionOrOwnerWindow_WhenEvaluated(
        TicketStatus status,
        int minutesAfterOpening,
        bool canCancelAny,
        bool isOwnerWhoCanEdit,
        string? expectedErrorCode)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(status);

        // Act
        Result result = new TicketSlaCycleService()
            .Cancel(ticket, [], canCancelAny, isOwnerWhoCanEdit, AgentId, OpenedAtUtc.AddMinutes(minutesAfterOpening));

        // Assert
        if (expectedErrorCode is null)
        {
            result.IsSuccess.ShouldBeTrue();
        }
        else
        {
            result.Error.Code.ShouldBe(expectedErrorCode);
        }
    }

    [Fact]
    public void Cancel_Should_ReturnWindowInError_WhenOwnerWindowHasPassed()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        Result result = new TicketSlaCycleService()
            .Cancel(ticket, [], canCancelAny: false, isOwnerWhoCanEdit: true, AgentId, OpenedAtUtc.AddMinutes(16));

        // Assert
        result.Error.ShouldBe(TicketErrors.CancelWindowExpired(Ticket.OwnerCancelWindowMinutes));
        ticket.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, TicketWaitingOn.Requester)]
    [InlineData(false, TicketWaitingOn.Agent)]
    public void StartProgress_Should_RecalculateWaitingOn_WhenStatusChangesToActive(bool lastMessageIsFromAgent, TicketWaitingOn expected)
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        ticket.StartProgress(lastMessageIsFromAgent, AgentId, OccurredAtUtc);

        // Assert
        ticket.WaitingOn.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Canceled)]
    public void ResolveAndCancel_Should_ClearWaitingOn_WhenTicketBecomesInactive(TicketStatus status)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.InProgress);

        // Act
        var slaCycle = new TicketSlaCycleService();
        _ = status == TicketStatus.Resolved
            ? slaCycle.Resolve(ticket, [], AgentId, OccurredAtUtc)
            : slaCycle.Cancel(ticket, [], canCancelAny: true, isOwnerWhoCanEdit: false, AgentId, OccurredAtUtc);

        // Assert
        ticket.WaitingOn.ShouldBeNull();
    }

    [Fact]
    public void ChangeDescription_Should_KeepWaitingOn_WhenOnlyDescriptionChanges()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        ticket.ChangeDescription("Updated description", AgentId, OccurredAtUtc);

        // Assert
        ticket.WaitingOn.ShouldBe(TicketWaitingOn.Agent);
    }

    [Fact]
    public void Close_Should_ClearWaitingOn_WhenTicketIsClosed()
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.Resolved);

        // Act
        ticket.Close(AgentId, OccurredAtUtc);

        // Assert
        ticket.WaitingOn.ShouldBeNull();
    }

    [Theory]
    [InlineData(true, TicketWaitingOn.Requester)]
    [InlineData(false, TicketWaitingOn.Agent)]
    public void Reopen_Should_RecalculateWaitingOn_WhenTicketIsReopened(bool lastMessageIsFromAgent, TicketWaitingOn expected)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.Resolved);

        // Act
        new TicketSlaCycleService().Reopen(ticket, [], Calendar, lastMessageIsFromAgent, AgentId, OccurredAtUtc);

        // Assert
        ticket.WaitingOn.ShouldBe(expected);
    }

    [Fact]
    public void ChangeDescription_Should_MoveLastActivityToTheHistoryEntry_WhenDescriptionChanges()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        ticket.ChangeDescription("Updated description", AgentId, OccurredAtUtc);

        // Assert
        ticket.LastActivityAtUtc.ShouldBe(OccurredAtUtc);
    }

    [Fact]
    public void ChangeDescription_Should_KeepLastActivity_WhenNothingChanges()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        ticket.ChangeDescription(ticket.Description, AgentId, OccurredAtUtc);

        // Assert
        ticket.LastActivityAtUtc.ShouldBe(OpenedAtUtc);
    }

    [Fact]
    public void RecordAttachmentActivity_Should_NeverMoveLastActivityBack_WhenEntryIsOlder()
    {
        // Arrange
        Ticket ticket = CreateTicket();
        ticket.Assign(new User { Id = AgentId }, AgentId, OccurredAtUtc);
        TicketHistoryEntry olderEntry = TicketTestData.CreateAttachment(ticket.Id, AgentId, OpenedAtUtc.AddMinutes(5)).RecordUpload();

        // Act
        ticket.RecordAttachmentActivity(olderEntry);

        // Assert
        ticket.LastActivityAtUtc.ShouldBe(OccurredAtUtc);
    }

    [Theory]
    [InlineData(true, TicketWaitingOn.Requester)]
    [InlineData(false, TicketWaitingOn.Agent)]
    public void RecordMessageActivity_Should_MoveLastActivityAndRecalculateWaitingOn_WhenTicketIsActive(
        bool lastMessageIsFromAgent,
        TicketWaitingOn expected)
    {
        // Arrange
        Ticket ticket = CreateTicket();
        TicketHistoryEntry posting = TicketTestData.CreateMessage(ticket.Id, AgentId, "Resposta", OpenedAtUtc)
            .Edit("Resposta corrigida", AgentId, OccurredAtUtc).Value;

        // Act
        ticket.RecordMessageActivity(posting, lastMessageIsFromAgent);

        // Assert
        ticket.LastActivityAtUtc.ShouldBe(OccurredAtUtc);
        ticket.WaitingOn.ShouldBe(expected);
    }

    [Fact]
    public void RecordMessageActivity_Should_KeepWaitingOnEmpty_WhenTicketIsInactive()
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.Resolved);
        TicketHistoryEntry posting = TicketTestData.CreateMessage(ticket.Id, AgentId, "Resposta", OpenedAtUtc)
            .Edit("Resposta corrigida", AgentId, OccurredAtUtc).Value;

        // Act
        ticket.RecordMessageActivity(posting, lastMessageIsFromAgent: true);

        // Assert
        ticket.WaitingOn.ShouldBeNull();
    }

    [Fact]
    public void PostMessage_Should_CreateMessageAndRecordMessageAdded_WhenCalled()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        (TicketMessage message, TicketHistoryEntry history) = ticket.PostMessage(AgentId, "Verificando", authorIsAgent: true, OccurredAtUtc);

        // Assert
        message.TicketId.ShouldBe(ticket.Id);
        message.AuthorUserId.ShouldBe(AgentId);
        message.Content.ShouldBe("Verificando");
        message.CreatedAtUtc.ShouldBe(OccurredAtUtc);
        message.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new TicketMessagePostedDomainEvent(message.Id, ticket.Id, AgentId));
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.MessageAdded);
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.LastActivityAtUtc.ShouldBe(OccurredAtUtc);
    }

    [Theory]
    [InlineData(true, false, TicketWaitingOn.Requester)]
    [InlineData(false, false, TicketWaitingOn.Agent)]
    [InlineData(true, true, TicketWaitingOn.Agent)]
    public void PostMessage_Should_WaitOnRequesterOnlyWhenAgentWhoIsNotRequesterAnswers(
        bool authorIsAgent,
        bool authorIsRequester,
        TicketWaitingOn expected)
    {
        // Arrange
        Ticket ticket = CreateTicket();
        Guid authorId = authorIsRequester ? ticket.CreatedByUserId : AgentId;

        // Act
        ticket.PostMessage(authorId, "Mensagem", authorIsAgent, OccurredAtUtc);

        // Assert
        ticket.WaitingOn.ShouldBe(expected);
    }

    [Fact]
    public void IsParticipant_Should_BeTrueOnlyForRequesterAndAssignee_WhenTicketIsAssigned()
    {
        // Arrange
        var assignee = new User { Id = Guid.NewGuid() };
        Ticket ticket = CreateTicket(assignee);

        // Act & Assert
        ticket.IsParticipant(ticket.CreatedByUserId).ShouldBeTrue();
        ticket.IsParticipant(assignee.Id).ShouldBeTrue();
        ticket.IsParticipant(Guid.NewGuid()).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, TicketSlaStatus.OnTime)]
    [InlineData(179, TicketSlaStatus.OnTime)]
    [InlineData(180, TicketSlaStatus.AtRisk)]
    [InlineData(239, TicketSlaStatus.AtRisk)]
    [InlineData(240, TicketSlaStatus.Breached)]
    [InlineData(600, TicketSlaStatus.Breached)]
    public void GetSlaStatus_Should_FollowWindow_WhenTicketIsNotResolved(int minutesAfterOpening, TicketSlaStatus expected)
    {
        // Arrange: prazo 4 h e alerta 3 h depois da abertura.
        Ticket ticket = CreateTicketIn(TicketStatus.InProgress);

        // Act
        TicketSlaStatus status = GetSlaStatus(ticket, OpenedAtUtc.AddMinutes(minutesAfterOpening));

        // Assert
        status.ShouldBe(expected);
    }

    [Theory]
    [InlineData(240, TicketSlaStatus.Met)]
    [InlineData(241, TicketSlaStatus.Missed)]
    public void GetSlaStatus_Should_FreezeResult_WhenTicketWasResolvedAndReopened(int resolvedMinutesAfterOpening, TicketSlaStatus expected)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.InProgress);
        var slaCycle = new TicketSlaCycleService();
        slaCycle.Resolve(ticket, [], AgentId, OpenedAtUtc.AddMinutes(resolvedMinutesAfterOpening));
        slaCycle.Reopen(ticket, [], Calendar, lastMessageIsFromAgent: false, AgentId, OpenedAtUtc.AddDays(1));

        // Act
        TicketSlaStatus status = GetSlaStatus(ticket, OpenedAtUtc.AddDays(10));

        // Assert
        status.ShouldBe(expected);
    }

    [Fact]
    public void GetSlaStatus_Should_ReturnNotApplicable_WhenTicketWasCanceledWithoutResolution()
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.Canceled);

        // Act
        TicketSlaStatus status = GetSlaStatus(ticket, OpenedAtUtc.AddDays(10));

        // Assert
        status.ShouldBe(TicketSlaStatus.NotApplicable);
    }

    [Theory]
    [InlineData(TicketStatus.Open, 10, false, true, true)]
    [InlineData(TicketStatus.Open, 15, false, true, true)]
    [InlineData(TicketStatus.Open, 16, false, true, false)]
    [InlineData(TicketStatus.InProgress, 20, true, false, true)]
    [InlineData(TicketStatus.Open, 10, false, false, false)]
    [InlineData(TicketStatus.Resolved, 10, true, true, false)]
    public void CanBeCanceled_Should_MatchCancel_WhenEvaluatedOnColumns(
        TicketStatus status,
        int minutesAfterOpening,
        bool canCancelAny,
        bool isOwnerWhoCanEdit,
        bool expected)
    {
        // Arrange
        Ticket ticket = CreateTicketIn(status);
        DateTime utcNow = OpenedAtUtc.AddMinutes(minutesAfterOpening);

        // Act
        bool canBeCanceled = Ticket.CanBeCanceled(ticket.Status, ticket.CreatedAtUtc, canCancelAny, isOwnerWhoCanEdit, utcNow);

        // Assert
        canBeCanceled.ShouldBe(expected);
        new TicketSlaCycleService().Cancel(ticket, [], canCancelAny, isOwnerWhoCanEdit, AgentId, utcNow).IsSuccess.ShouldBe(expected);
    }

    private static TicketSlaStatus GetSlaStatus(Ticket ticket, DateTime utcNow) =>
        Ticket.GetSlaStatus(ticket.Status, ticket.DueAtUtc, ticket.SlaWarningAtUtc, ticket.ResolvedAtUtc, utcNow);

    private static Ticket CreateTicket(User? assignee = null, string description = "The printer on the 3rd floor is not working")
    {
        var requester = new User { Id = Guid.NewGuid() };

        var ticket = Ticket.Create(
            "Printer is broken",
            description,
            TicketPriority.Medium,
            Helpdesk,
            requester,
            assignee,
            Plan,
            Calendar,
            requester.Id,
            OpenedAtUtc);

        ticket.ClearDomainEvents();

        return ticket;
    }

    // Leva o chamado ao status pelo caminho permitido e descarta os eventos do arranjo.
    private static Ticket CreateTicketIn(TicketStatus status, User? assignee = null)
    {
        Ticket ticket = CreateTicket(assignee);
        var slaCycle = new TicketSlaCycleService();

        if (status is TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, OpenedAtUtc);
        }

        if (status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            slaCycle.Resolve(ticket, [], AgentId, OpenedAtUtc.AddMinutes(30));
        }

        if (status == TicketStatus.Closed)
        {
            ticket.Close(AgentId, OpenedAtUtc.AddMinutes(45));
        }

        if (status == TicketStatus.Canceled)
        {
            slaCycle.Cancel(ticket, [], canCancelAny: true, isOwnerWhoCanEdit: false, AgentId, OpenedAtUtc);
        }

        ticket.ClearDomainEvents();

        return ticket;
    }
}
