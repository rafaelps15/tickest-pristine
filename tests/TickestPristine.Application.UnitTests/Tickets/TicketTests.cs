using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

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

    private static readonly TicketSlaPlan Plan = new(null, "Padrão", 1, Guid.NewGuid(), 30, 240, IsLegacy: false);

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
            DueAtUtc,
            SlaWarningAtUtc,
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
            DueAtUtc,
            SlaWarningAtUtc,
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
        TicketHistory history = ticket.RecordOpening(openedByUserId);

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
    [InlineData(TicketStatus.Open, true)]
    [InlineData(TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Canceled, false)]
    public void IsActiveStatus_Should_BeTrueOnlyForOpenAndInProgress_WhenStatusIsGiven(TicketStatus status, bool expected)
    {
        // Act
        bool isActive = Ticket.IsActiveStatus(status);

        // Assert
        isActive.ShouldBe(expected);
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
    public void Update_Should_RecordOnlyStatusChange_WhenOnlyStatusChanges()
    {
        // Arrange
        Ticket ticket = CreateTicket();
        string description = ticket.Description;

        // Act
        IReadOnlyList<TicketHistory> entries = ticket.Update(description, TicketStatus.InProgress, AgentId, OccurredAtUtc);

        // Assert
        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.Description.ShouldBe(description);
        TicketHistory history = entries.ShouldHaveSingleItem();
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.StatusChanged);
        history.OldValue.ShouldBe(nameof(TicketStatus.Open));
        history.NewValue.ShouldBe(nameof(TicketStatus.InProgress));
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketUpdatedDomainEvent>().ShouldContain(e => e.TicketId == ticket.Id);
        ticket.DomainEvents.OfType<TicketStatusChangedDomainEvent>().ShouldContain(e =>
            e.TicketId == ticket.Id &&
            e.OldStatus == TicketStatus.Open &&
            e.NewStatus == TicketStatus.InProgress &&
            e.ChangedByUserId == AgentId);
    }

    [Fact]
    public void Update_Should_RecordOnlyDescriptionChange_WhenOnlyDescriptionChanges()
    {
        // Arrange
        Ticket ticket = CreateTicket(description: "Original description");

        // Act
        IReadOnlyList<TicketHistory> entries = ticket.Update("Updated description", TicketStatus.Open, AgentId, OccurredAtUtc);

        // Assert
        ticket.Description.ShouldBe("Updated description");
        ticket.Status.ShouldBe(TicketStatus.Open);
        TicketHistory history = entries.ShouldHaveSingleItem();
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.DescriptionChanged);
        history.OldValue.ShouldBe("Original description");
        history.NewValue.ShouldBe("Updated description");
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketUpdatedDomainEvent>().ShouldContain(e => e.TicketId == ticket.Id);
        ticket.DomainEvents.OfType<TicketStatusChangedDomainEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Update_Should_RecordStatusBeforeDescription_WhenBothChange()
    {
        // Arrange
        Ticket ticket = CreateTicket(description: "Original description");

        // Act
        IReadOnlyList<TicketHistory> entries = ticket.Update("Updated description", TicketStatus.InProgress, AgentId, OccurredAtUtc);

        // Assert
        entries.Select(e => e.Action).ShouldBe([TicketHistoryAction.StatusChanged, TicketHistoryAction.DescriptionChanged]);
        entries[0].OldValue.ShouldBe(nameof(TicketStatus.Open));
        entries[0].NewValue.ShouldBe(nameof(TicketStatus.InProgress));
        entries[1].OldValue.ShouldBe("Original description");
        entries[1].NewValue.ShouldBe("Updated description");
        entries.ShouldAllBe(e => e.TicketId == ticket.Id && e.ChangedByUserId == AgentId && e.OccurredAtUtc == OccurredAtUtc);
        ticket.DomainEvents.Select(e => e.GetType()).ShouldBe(
            [typeof(TicketUpdatedDomainEvent), typeof(TicketStatusChangedDomainEvent)]);
    }

    [Fact]
    public void Update_Should_RecordNothingAndStillRaiseUpdatedEvent_WhenNothingChanges()
    {
        // Arrange
        Ticket ticket = CreateTicket();

        // Act
        IReadOnlyList<TicketHistory> entries = ticket.Update(ticket.Description, ticket.Status, AgentId, OccurredAtUtc);

        // Assert
        entries.ShouldBeEmpty();
        ticket.DomainEvents.OfType<TicketUpdatedDomainEvent>().ShouldContain(e => e.TicketId == ticket.Id);
        ticket.DomainEvents.OfType<TicketStatusChangedDomainEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Update_Should_RecordResolutionTime_WhenTicketIsResolvedForTheFirstTime()
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.InProgress);

        // Act
        ticket.Update(ticket.Description, TicketStatus.Resolved, AgentId, OccurredAtUtc);

        // Assert
        ticket.Status.ShouldBe(TicketStatus.Resolved);
        ticket.ResolvedAtUtc.ShouldBe(OccurredAtUtc);
    }

    [Fact]
    public void Update_Should_KeepFirstResolutionTime_WhenReopenedTicketIsResolvedAgain()
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.Resolved);
        DateTime firstResolutionUtc = ticket.ResolvedAtUtc!.Value;
        ticket.Reopen(AgentId, OccurredAtUtc);
        ticket.Update(ticket.Description, TicketStatus.InProgress, AgentId, OccurredAtUtc);

        // Act
        ticket.Update(ticket.Description, TicketStatus.Resolved, AgentId, OccurredAtUtc.AddHours(1));

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
        TicketHistory history = ticket.Assign(assignee, AgentId, OccurredAtUtc);

        // Assert
        ticket.AssignedToUserId.ShouldBe(assignee.Id);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.Assigned);
        history.OldValue.ShouldBeNull();
        history.NewValue.ShouldBe(assignee.Id.ToString());
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketReassignedDomainEvent>().ShouldContain(e =>
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
        TicketHistory history = ticket.Assign(assignee, AgentId, OccurredAtUtc);

        // Assert
        ticket.AssignedToUserId.ShouldBe(assignee.Id);
        history.TicketId.ShouldBe(ticket.Id);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.Action.ShouldBe(TicketHistoryAction.Reassigned);
        history.OldValue.ShouldBe(previous.Id.ToString());
        history.NewValue.ShouldBe(assignee.Id.ToString());
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
        ticket.DomainEvents.OfType<TicketReassignedDomainEvent>().ShouldContain(e =>
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
        TicketHistory history = ticket.Unassign(AgentId, OccurredAtUtc);

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

    [Fact]
    public void Close_Should_CloseAndRecordStatusChange_WhenTicketIsResolved()
    {
        // Arrange
        Ticket ticket = CreateTicketIn(TicketStatus.Resolved);

        // Act
        TicketHistory history = ticket.Close(AgentId, OccurredAtUtc);

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
        TicketHistory history = ticket.Reopen(AgentId, OccurredAtUtc);

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
        TicketHistory history = ticket.Delete(AgentId, OccurredAtUtc);

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
            DueAtUtc,
            SlaWarningAtUtc,
            requester.Id,
            OpenedAtUtc);

        ticket.ClearDomainEvents();

        return ticket;
    }

    // Leva o chamado ao status pelo caminho permitido e descarta os eventos do arranjo.
    private static Ticket CreateTicketIn(TicketStatus status)
    {
        Ticket ticket = CreateTicket();

        if (status is TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.Update(ticket.Description, TicketStatus.InProgress, AgentId, OpenedAtUtc);
        }

        if (status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.Update(ticket.Description, TicketStatus.Resolved, AgentId, OpenedAtUtc.AddMinutes(30));
        }

        if (status == TicketStatus.Closed)
        {
            ticket.Close(AgentId, OpenedAtUtc.AddMinutes(45));
        }

        if (status == TicketStatus.Canceled)
        {
            ticket.Update(ticket.Description, TicketStatus.Canceled, AgentId, OpenedAtUtc);
        }

        ticket.ClearDomainEvents();

        return ticket;
    }
}
