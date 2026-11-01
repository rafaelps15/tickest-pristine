using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketSlaCycleServiceTests
{
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OccurredAtUtc = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AgentId = Guid.NewGuid();

    // Calendário 24 horas: a resolução de 240 minutos vence 4 h depois do início, e o alerta, 3 h depois.
    private static readonly TicketSlaPlan Plan = new(null, "Padrão", 1, SlaCalendarIds.Continuous, 30, 240, IsLegacy: false);

    private static readonly BusinessCalendar Calendar = ContinuousSlaCalendar.Create();

    private readonly TicketSlaCycleService _slaCycle = new();

    [Fact]
    public void Start_Should_CreateCycleOneTimersWithTicketDeadlines_WhenPlanHasFirstResponse()
    {
        // Arrange
        Ticket ticket = CreateTicket(Plan);

        // Act
        IReadOnlyList<TicketSlaTimer> timers = _slaCycle.Start(ticket, Calendar);

        // Assert
        timers.ShouldAllBe(t => t.TicketId == ticket.Id && t.Cycle == 1 && t.StartedAtUtc == OpenedAtUtc && t.IsOpen);

        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        resolution.DueOnUtc.ShouldBe(ticket.DueAtUtc);
        resolution.WarningOnUtc.ShouldBe(ticket.SlaWarningAtUtc);

        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.DueOnUtc.ShouldBe(OpenedAtUtc.AddMinutes(30));
        firstResponse.WarningOnUtc.ShouldBe(OpenedAtUtc.AddMinutes(22.5));
    }

    [Fact]
    public void Start_Should_CreateOnlyResolutionTimer_WhenPlanHasNoFirstResponseTarget()
    {
        // Arrange
        Ticket ticket = CreateTicket(TicketSlaPlan.Legacy(TicketPriority.Urgent));

        // Act
        IReadOnlyList<TicketSlaTimer> timers = _slaCycle.Start(ticket, Calendar);

        // Assert
        TicketSlaTimer resolution = timers.ShouldHaveSingleItem();
        resolution.Metric.ShouldBe(SlaMetric.Resolution);
        resolution.DueOnUtc.ShouldBe(ticket.DueAtUtc);
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Legacy")]
    [InlineData("Continuous")]
    public void Start_Should_TakeTargetAndCalendarOfEachTimerFromThePlan(string planName)
    {
        // Arrange
        TicketSlaPlan plan = PlanNamed(planName);
        Ticket ticket = CreateTicket(plan);

        // Act
        IReadOnlyList<TicketSlaTimer> timers = _slaCycle.Start(ticket, Calendar);

        // Assert
        timers.ShouldAllBe(t => t.CalendarId == plan.CalendarId);
        timers.Single(t => t.Metric == SlaMetric.Resolution).TargetMinutes.ShouldBe(plan.ResolutionMinutes);

        if (plan.FirstResponseMinutes is { } firstResponseMinutes)
        {
            timers.Single(t => t.Metric == SlaMetric.FirstResponse).TargetMinutes.ShouldBe(firstResponseMinutes);
        }
        else
        {
            timers.ShouldNotContain(t => t.Metric == SlaMetric.FirstResponse);
        }
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Legacy")]
    [InlineData("Continuous")]
    public void Reopen_Should_TakeTargetAndCalendarOfNewCycleFromThePlan(string planName)
    {
        // Arrange
        TicketSlaPlan plan = PlanNamed(planName);
        Ticket ticket = CreateTicket(plan);
        List<TicketSlaTimer> timers = [.. _slaCycle.Start(ticket, Calendar)];
        ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, OpenedAtUtc.AddMinutes(5));
        CloseCycle(ticket, timers, TicketStatus.Resolved);

        // Act
        Result<TicketReopening> result = _slaCycle.Reopen(ticket, timers, Calendar, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc.AddHours(1));

        // Assert
        TicketSlaTimer cycle = result.Value.ResolutionCycle;
        cycle.Metric.ShouldBe(SlaMetric.Resolution);
        cycle.TargetMinutes.ShouldBe(plan.ResolutionMinutes);
        cycle.CalendarId.ShouldBe(plan.CalendarId);
    }

    [Fact]
    public void Resolve_Should_CompleteCurrentCyclesWithResolutionCause_AndIgnoreTimersOfAnotherTicket()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        (_, List<TicketSlaTimer> otherTimers) = Open();

        // Act
        Result<TicketHistory> result = _slaCycle.Resolve(ticket, [.. timers, .. otherTimers], AgentId, OccurredAtUtc);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ticket.Status.ShouldBe(TicketStatus.Resolved);
        ticket.ResolvedAtUtc.ShouldBe(OccurredAtUtc);
        timers.ShouldAllBe(t => t.CompletedOnUtc == OccurredAtUtc && t.CompletionCause == SlaCompletionCause.Resolution);
        otherTimers.ShouldAllBe(t => t.IsOpen && t.NextActionOnUtc == t.DueOnUtc);
    }

    [Fact]
    public void Cancel_Should_StopOpenTimers_AndIgnoreTimersOfAnotherTicket()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        (_, List<TicketSlaTimer> otherTimers) = Open();

        // Act
        Result<TicketHistory> result = _slaCycle.Cancel(
            ticket,
            [.. timers, .. otherTimers],
            canCancelAny: true,
            isOwnerWhoCanEdit: false,
            AgentId,
            OccurredAtUtc);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ticket.Status.ShouldBe(TicketStatus.Canceled);
        timers.ShouldAllBe(t => t.StoppedOnUtc == OccurredAtUtc && t.NextActionOnUtc == null);
        otherTimers.ShouldAllBe(t => t.IsOpen && t.NextActionOnUtc == t.DueOnUtc);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Canceled)]
    public void Reopen_Should_OpenNewResolutionCycleWithFullTarget_AndKeepTicketDeadlines(TicketStatus status)
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        CloseCycle(ticket, timers, status);
        (DateTime dueAtUtc, DateTime warningAtUtc, DateTime? resolvedAtUtc) = (ticket.DueAtUtc, ticket.SlaWarningAtUtc, ticket.ResolvedAtUtc);
        TimerState[] timersBefore = Capture(timers);
        DateTime reopenedAtUtc = OccurredAtUtc.AddHours(2);

        // Act
        Result<TicketReopening> result = _slaCycle.Reopen(
            ticket,
            timers,
            Calendar,
            lastMessageIsFromAgent: false,
            AgentId,
            reopenedAtUtc);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.DueAtUtc.ShouldBe(dueAtUtc);
        ticket.SlaWarningAtUtc.ShouldBe(warningAtUtc);
        ticket.ResolvedAtUtc.ShouldBe(resolvedAtUtc);
        result.Value.History.Action.ShouldBe(TicketHistoryAction.Reopened);

        TicketSlaTimer cycle = result.Value.ResolutionCycle;
        cycle.TicketId.ShouldBe(ticket.Id);
        cycle.Metric.ShouldBe(SlaMetric.Resolution);
        cycle.Cycle.ShouldBe(2);
        cycle.TargetMinutes.ShouldBe(240);
        cycle.StartedAtUtc.ShouldBe(reopenedAtUtc);
        cycle.WarningOnUtc.ShouldBe(reopenedAtUtc.AddHours(3));
        cycle.DueOnUtc.ShouldBe(reopenedAtUtc.AddHours(4));
        Capture(timers).ShouldBe(timersBefore);
    }

    [Fact]
    public void Resolve_Should_KeepFirstResponseAnsweredByMessage_WhenItIsAlreadyCompleted()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, OpenedAtUtc.AddMinutes(10));

        // Act
        _slaCycle.Resolve(ticket, timers, AgentId, OccurredAtUtc);

        // Assert
        firstResponse.CompletedOnUtc.ShouldBe(OpenedAtUtc.AddMinutes(10));
        firstResponse.CompletionCause.ShouldBe(SlaCompletionCause.Message);
        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        resolution.CompletedOnUtc.ShouldBe(OccurredAtUtc);
        resolution.CompletionCause.ShouldBe(SlaCompletionCause.Resolution);
    }

    [Fact]
    public void Reopen_Should_NumberNewCycleAfterHighestCycle_AndIgnoreTimersOfAnotherTicket()
    {
        // Arrange: ciclo 1 parado no cancelamento, ciclo 2 concluído na resolução; o outro chamado continua aberto.
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        CloseCycle(ticket, timers, TicketStatus.Canceled);
        timers.Add(_slaCycle.Reopen(ticket, timers, Calendar, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc).Value.ResolutionCycle);
        ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, OccurredAtUtc);
        CloseCycle(ticket, timers, TicketStatus.Resolved);
        (_, List<TicketSlaTimer> otherTimers) = Open();
        TimerState[] otherTimersBefore = Capture(otherTimers);

        // Act
        Result<TicketReopening> result = _slaCycle.Reopen(
            ticket,
            [.. timers, .. otherTimers],
            Calendar,
            lastMessageIsFromAgent: false,
            AgentId,
            OccurredAtUtc.AddHours(1));

        // Assert
        result.Value.ResolutionCycle.Cycle.ShouldBe(3);
        Capture(otherTimers).ShouldBe(otherTimersBefore);
    }

    [Fact]
    public void Reopen_Should_StartCycleOne_WhenTicketHasNoResolutionCycle()
    {
        // Arrange
        (Ticket ticket, _) = OpenInProgress();
        CloseCycle(ticket, [], TicketStatus.Resolved);

        // Act
        Result<TicketReopening> result = _slaCycle.Reopen(ticket, [], Calendar, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc.AddHours(1));

        // Assert
        result.Value.ResolutionCycle.Cycle.ShouldBe(1);
    }

    [Fact]
    public void Delete_Should_DequeueOnlyQueuedTimers_WithoutStopOrBreach()
    {
        // Arrange: a primeira resposta concluída depois do prazo continua na fila, à espera da violação.
        (Ticket ticket, List<TicketSlaTimer> timers) = Open();
        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.CompleteFirstResponse(ticket, AgentId, authorIsAgent: true, OpenedAtUtc.AddMinutes(45));
        (_, List<TicketSlaTimer> otherTimers) = Open();
        TimerState[] otherTimersBefore = Capture(otherTimers);

        // Act
        Result<TicketHistory> result = _slaCycle.Delete(ticket, [.. timers, .. otherTimers], AgentId, OccurredAtUtc);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        timers.ShouldAllBe(t => t.NextActionOnUtc == null && t.StoppedOnUtc == null && t.BreachedOnUtc == null && !t.DomainEvents.Any());
        firstResponse.CompletedOnUtc.ShouldBe(OpenedAtUtc.AddMinutes(45));
        Capture(otherTimers).ShouldBe(otherTimersBefore);
    }

    [Fact]
    public void Reopen_Should_FailWithoutAnyChange_WhenResolutionCycleIsStillOpen()
    {
        // Arrange: chamado resolvido sem os cronômetros, que ficaram em aberto (dado inconsistente).
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        _slaCycle.Resolve(ticket, [], AgentId, OccurredAtUtc);
        ticket.ClearDomainEvents();

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Reopen(ticket, timers, Calendar, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc.AddHours(1)),
            TicketErrors.ResolutionCycleAlreadyOpen(ticket.Id));
    }

    [Fact]
    public void Resolve_Should_FailWithoutAnyChange_WhenTicketIsOpen()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = Open();

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Resolve(ticket, timers, AgentId, OccurredAtUtc),
            TicketErrors.InvalidStatusTransition(TicketStatus.Open, TicketStatus.Resolved));
    }

    [Fact]
    public void Resolve_Should_FailWithoutAnyChange_WhenTicketIsInactive()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        CloseCycle(ticket, timers, TicketStatus.Canceled);

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Resolve(ticket, timers, AgentId, OccurredAtUtc.AddHours(1)),
            TicketErrors.NotActive(ticket.Id));
    }

    [Fact]
    public void Cancel_Should_FailWithoutAnyChange_WhenOwnerWindowHasPassed()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = Open();

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Cancel(ticket, timers, canCancelAny: false, isOwnerWhoCanEdit: true, ticket.CreatedByUserId, OpenedAtUtc.AddMinutes(16)),
            TicketErrors.CancelWindowExpired(Ticket.OwnerCancelWindowMinutes));
    }

    [Fact]
    public void Cancel_Should_FailWithoutAnyChange_WhenCallerCannotCancel()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = Open();

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Cancel(ticket, timers, canCancelAny: false, isOwnerWhoCanEdit: false, AgentId, OpenedAtUtc.AddMinutes(5)),
            TicketErrors.CancelForbidden);
    }

    [Fact]
    public void Cancel_Should_FailWithoutAnyChange_WhenTicketIsInactive()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        CloseCycle(ticket, timers, TicketStatus.Resolved);

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, AgentId, OccurredAtUtc.AddHours(1)),
            TicketErrors.NotActive(ticket.Id));
    }

    [Fact]
    public void Reopen_Should_FailWithoutAnyChange_WhenTicketIsActive()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Reopen(ticket, timers, Calendar, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc),
            TicketErrors.AlreadyActive(ticket.Id));
    }

    [Fact]
    public void Reopen_Should_FailWithoutAnyChange_WhenTransitionToOpenIsNotAllowed()
    {
        // Arrange: um status inativo sem transição para Aberto, que nenhum caminho da entidade produz.
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        CloseCycle(ticket, timers, TicketStatus.Resolved);
        var unknownStatus = (TicketStatus)99;
        ticket.With(nameof(Ticket.Status), unknownStatus);

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Reopen(ticket, timers, Calendar, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc.AddHours(1)),
            TicketErrors.InvalidStatusTransition(unknownStatus, TicketStatus.Open));
    }

    [Fact]
    public void Delete_Should_FailWithoutAnyChange_WhenTicketIsInactive()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        CloseCycle(ticket, timers, TicketStatus.Resolved);

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Delete(ticket, timers, AgentId, OccurredAtUtc.AddHours(1)),
            TicketErrors.NotActive(ticket.Id));
        ticket.DeletedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Reopen_Should_FailWithAlreadyActiveWithoutCalculating_WhenTicketIsActiveAndCalendarHasNoBusinessTime()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        var calendarWithoutBusinessTime = new BusinessCalendar([], TimeZoneInfo.Utc, is24x7: false, _ => new HashSet<DateOnly>());

        // Act & Assert
        ShouldFailWithoutAnyChange(
            ticket,
            timers,
            () => _slaCycle.Reopen(ticket, timers, calendarWithoutBusinessTime, lastMessageIsFromAgent: false, AgentId, OccurredAtUtc),
            TicketErrors.AlreadyActive(ticket.Id));
    }

    [Fact]
    public void Reopen_Should_ThrowBeforeAnyChange_WhenCalendarHasNoBusinessTime()
    {
        // Arrange
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenInProgress();
        CloseCycle(ticket, timers, TicketStatus.Resolved);
        var calendarWithoutBusinessTime = new BusinessCalendar([], TimeZoneInfo.Utc, is24x7: false, _ => new HashSet<DateOnly>());
        TicketState ticketBefore = Capture(ticket);
        TimerState[] timersBefore = Capture(timers);

        // Act
        Action reopen = () => _slaCycle.Reopen(
            ticket,
            timers,
            calendarWithoutBusinessTime,
            lastMessageIsFromAgent: false,
            AgentId,
            OccurredAtUtc.AddHours(1));

        // Assert
        reopen.ShouldThrow<InvalidOperationException>();
        Capture(ticket).ShouldBe(ticketBefore);
        Capture(timers).ShouldBe(timersBefore);
    }

    // Em falha, o chamado e cada cronômetro ficam como antes, sem eventos e sem cronômetro novo.
    private static void ShouldFailWithoutAnyChange(
        Ticket ticket,
        List<TicketSlaTimer> timers,
        Func<Result> operation,
        Error expectedError)
    {
        TicketState ticketBefore = Capture(ticket);
        TimerState[] timersBefore = Capture(timers);
        int timerCount = timers.Count;

        Result result = operation();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(expectedError);
        Capture(ticket).ShouldBe(ticketBefore);
        Capture(timers).ShouldBe(timersBefore);
        timers.Count.ShouldBe(timerCount);
    }

    private static TicketState Capture(Ticket ticket) => new(
        ticket.Status,
        ticket.ResolvedAtUtc,
        ticket.LastActivityAtUtc,
        ticket.WaitingOn,
        ticket.DueAtUtc,
        ticket.SlaWarningAtUtc,
        ticket.DomainEvents.Count);

    private static TimerState[] Capture(IEnumerable<TicketSlaTimer> timers) =>
    [
        .. timers.Select(t => new TimerState(
            t.Id,
            t.CompletedOnUtc,
            t.CompletionCause,
            t.StoppedOnUtc,
            t.NextActionOnUtc,
            t.DomainEvents.Count))
    ];

    // Fecha o ciclo 1 resolvendo ou cancelando o chamado em andamento, com os eventos do arranjo descartados.
    private void CloseCycle(Ticket ticket, List<TicketSlaTimer> timers, TicketStatus status)
    {
        _ = status == TicketStatus.Resolved
            ? _slaCycle.Resolve(ticket, timers, AgentId, OccurredAtUtc)
            : _slaCycle.Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, AgentId, OccurredAtUtc);

        ticket.ClearDomainEvents();
    }

    private (Ticket Ticket, List<TicketSlaTimer> Timers) Open()
    {
        Ticket ticket = CreateTicket(Plan);

        return (ticket, [.. _slaCycle.Start(ticket, Calendar)]);
    }

    private (Ticket Ticket, List<TicketSlaTimer> Timers) OpenInProgress()
    {
        (Ticket ticket, List<TicketSlaTimer> timers) = Open();
        ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, OpenedAtUtc.AddMinutes(5));
        ticket.ClearDomainEvents();

        return (ticket, timers);
    }

    private static TicketSlaPlan PlanNamed(string planName) => planName switch
    {
        "Default" => TicketSlaPlan.Default(TicketPriority.High),
        "Legacy" => TicketSlaPlan.Legacy(TicketPriority.High),
        _ => Plan
    };

    private static Ticket CreateTicket(TicketSlaPlan plan)
    {
        var requester = new User { Id = Guid.NewGuid() };

        var ticket = Ticket.Create(
            "Impressora parada",
            "A impressora do terceiro andar não liga",
            TicketPriority.Urgent,
            TicketTestData.CreateSector(),
            requester,
            assignee: null,
            plan,
            Calendar,
            requester.Id,
            OpenedAtUtc);

        ticket.ClearDomainEvents();

        return ticket;
    }

    private sealed record TicketState(
        TicketStatus Status,
        DateTime? ResolvedAtUtc,
        DateTime LastActivityAtUtc,
        TicketWaitingOn? WaitingOn,
        DateTime DueAtUtc,
        DateTime SlaWarningAtUtc,
        int EventCount);

    private sealed record TimerState(
        Guid Id,
        DateTime? CompletedOnUtc,
        SlaCompletionCause? CompletionCause,
        DateTime? StoppedOnUtc,
        DateTime? NextActionOnUtc,
        int EventCount);
}
