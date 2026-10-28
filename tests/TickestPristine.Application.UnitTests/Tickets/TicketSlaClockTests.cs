using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketSlaClockTests
{
    private static readonly BusinessCalendar Calendar = DefaultSlaCalendar.Create();

    private static readonly TicketSlaPlan UrgentPlan =
        new(null, "Padrão", 1, SlaCalendarIds.Default, 30, 240, IsLegacy: false);

    // Segunda-feira, 26/10/2026, em São Paulo (UTC-3).
    private static DateTime SaoPaulo(int hour, int minute, int second = 0) =>
        new(2026, 10, 26, hour + 3, minute, second, DateTimeKind.Utc);

    private static TicketSlaTimer UrgentResolution() =>
        TicketSlaClock.Start(Guid.NewGuid(), UrgentPlan, Calendar, SaoPaulo(8, 10)).Single(t => t.Metric == SlaMetric.Resolution);

    private static TicketSlaTimer UrgentFirstResponse() =>
        TicketSlaClock.Start(Guid.NewGuid(), UrgentPlan, Calendar, SaoPaulo(8, 10)).Single(t => t.Metric == SlaMetric.FirstResponse);

    [Fact]
    public void Start_Should_CreateCycleOneTimersWithDeadlinesInBusinessTime()
    {
        // Act
        List<TicketSlaTimer> timers = TicketSlaClock.Start(Guid.NewGuid(), UrgentPlan, Calendar, SaoPaulo(8, 10));

        // Assert
        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.Cycle.ShouldBe(1);
        firstResponse.TargetMinutes.ShouldBe(30);
        firstResponse.WarningOnUtc.ShouldBe(SaoPaulo(8, 32, 30));
        firstResponse.DueOnUtc.ShouldBe(SaoPaulo(8, 40));

        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        resolution.Cycle.ShouldBe(1);
        resolution.WarningOnUtc.ShouldBe(SaoPaulo(11, 10));
        resolution.DueOnUtc.ShouldBe(SaoPaulo(12, 10));
    }

    [Fact]
    public void Start_Should_SkipFirstResponse_WhenPlanHasNoFirstResponseTarget()
    {
        // Arrange
        TicketSlaPlan legacyPlan = TicketSla.LegacyPlan(TicketPriority.Urgent);

        // Act
        List<TicketSlaTimer> timers = TicketSlaClock.Start(Guid.NewGuid(), legacyPlan, ContinuousSlaCalendar.Create(), SaoPaulo(8, 10));

        // Assert
        TicketSlaTimer resolution = timers.ShouldHaveSingleItem();
        resolution.Metric.ShouldBe(SlaMetric.Resolution);
        resolution.DueOnUtc.ShouldBe(SaoPaulo(12, 10));
    }

    [Fact]
    public void GetState_Should_ReturnAtRiskWithConsumedAndRemaining_WhenBetweenWarningAndDue()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        DateTime nowUtc = SaoPaulo(11, 15);

        // Act
        SlaObjectiveState state = TicketSlaClock.GetState(resolution, nowUtc);
        int consumed = TicketSlaClock.ToWholeMinutes(TicketSlaClock.GetConsumed(resolution, Calendar, nowUtc));

        // Assert
        state.ShouldBe(SlaObjectiveState.AtRisk);
        consumed.ShouldBe(185);
        TicketSlaClock.GetRemainingMinutes(resolution.TargetMinutes, consumed).ShouldBe(55);
    }

    [Fact]
    public void GetState_Should_ReturnBreachedWithZeroRemaining_WhenPastDueAndOpen()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        DateTime nowUtc = SaoPaulo(12, 20);

        // Act
        SlaObjectiveState state = TicketSlaClock.GetState(resolution, nowUtc);
        int consumed = TicketSlaClock.ToWholeMinutes(TicketSlaClock.GetConsumed(resolution, Calendar, nowUtc));

        // Assert
        state.ShouldBe(SlaObjectiveState.Breached);
        consumed.ShouldBe(250);
        TicketSlaClock.GetRemainingMinutes(resolution.TargetMinutes, consumed).ShouldBe(0);
    }

    [Fact]
    public void GetState_Should_ReturnRunning_WhenBeforeWarning()
    {
        // Act
        SlaObjectiveState state = TicketSlaClock.GetState(UrgentResolution(), SaoPaulo(11, 9, 59));

        // Assert
        state.ShouldBe(SlaObjectiveState.Running);
    }

    [Fact]
    public void GetState_Should_ReturnMet_WhenCompletedExactlyAtDue()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        TicketSlaClock.Complete(resolution, SaoPaulo(12, 10), SlaCompletionCause.Resolution);

        // Act
        SlaObjectiveState state = TicketSlaClock.GetState(resolution, SaoPaulo(13, 0));

        // Assert
        state.ShouldBe(SlaObjectiveState.Met);
    }

    [Fact]
    public void GetState_Should_ReturnMissed_WhenCompletedAfterDue()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        TicketSlaClock.Complete(resolution, SaoPaulo(12, 30), SlaCompletionCause.Resolution);

        // Act
        SlaObjectiveState state = TicketSlaClock.GetState(resolution, SaoPaulo(13, 0));

        // Assert
        state.ShouldBe(SlaObjectiveState.Missed);
    }

    [Fact]
    public void GetState_Should_ReturnStopped_WhenStoppedEvenAfterDue()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        TicketSlaClock.Stop(resolution, SaoPaulo(9, 0));

        // Act
        SlaObjectiveState state = TicketSlaClock.GetState(resolution, SaoPaulo(17, 0));
        int consumed = TicketSlaClock.ToWholeMinutes(TicketSlaClock.GetConsumed(resolution, Calendar, SaoPaulo(17, 0)));

        // Assert
        state.ShouldBe(SlaObjectiveState.Stopped);
        consumed.ShouldBe(50);
    }

    [Fact]
    public void Complete_Should_KeepFirstCompletion_WhenCalledAgain()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();
        TicketSlaClock.Complete(resolution, SaoPaulo(10, 0), SlaCompletionCause.Resolution);

        // Act
        TicketSlaClock.Complete(resolution, SaoPaulo(11, 0), SlaCompletionCause.Resolution);
        TicketSlaClock.Stop(resolution, SaoPaulo(11, 30));

        // Assert
        resolution.CompletedOnUtc.ShouldBe(SaoPaulo(10, 0));
        resolution.StoppedOnUtc.ShouldBeNull();
    }

    [Fact]
    public void GetConsumed_Should_TruncateFractionOfMinute()
    {
        // Arrange: no alerta da primeira resposta, 22 minutos e 30 segundos consumidos.
        TicketSlaTimer firstResponse = UrgentFirstResponse();
        DateTime nowUtc = SaoPaulo(8, 32, 30);

        // Act
        int consumed = TicketSlaClock.ToWholeMinutes(TicketSlaClock.GetConsumed(firstResponse, Calendar, nowUtc));

        // Assert
        consumed.ShouldBe(22);
        TicketSlaClock.GetRemainingMinutes(firstResponse.TargetMinutes, consumed).ShouldBe(8);
        TicketSlaClock.GetState(firstResponse, nowUtc).ShouldBe(SlaObjectiveState.AtRisk);
    }

    [Fact]
    public void StartResolutionCycle_Should_StartNewCycleWithFullTarget()
    {
        // Act
        TicketSlaTimer cycle = TicketSlaClock.StartResolutionCycle(Guid.NewGuid(), UrgentPlan, 2, Calendar, SaoPaulo(14, 0));

        // Assert
        cycle.Metric.ShouldBe(SlaMetric.Resolution);
        cycle.Cycle.ShouldBe(2);
        cycle.TargetMinutes.ShouldBe(240);
        cycle.StartedAtUtc.ShouldBe(SaoPaulo(14, 0));
        cycle.DueOnUtc.ShouldBe(SaoPaulo(18, 0));
    }

    [Fact]
    public void CurrentCycle_Should_ReturnHighestCycleOfMetric()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        List<TicketSlaTimer> timers = TicketSlaClock.Start(ticketId, UrgentPlan, Calendar, SaoPaulo(8, 10));
        timers.Add(TicketSlaClock.StartResolutionCycle(ticketId, UrgentPlan, 2, Calendar, SaoPaulo(14, 0)));

        // Act
        TicketSlaTimer? current = TicketSlaClock.CurrentCycle(timers, SlaMetric.Resolution);

        // Assert
        current.ShouldNotBeNull().Cycle.ShouldBe(2);
        TicketSlaClock.CurrentCycle([], SlaMetric.FirstResponse).ShouldBeNull();
    }

    [Fact]
    public void Start_Should_QueueEachTimerAtItsDeadline()
    {
        // Act
        List<TicketSlaTimer> timers = TicketSlaClock.Start(Guid.NewGuid(), UrgentPlan, Calendar, SaoPaulo(8, 10));

        // Assert
        timers.ShouldAllBe(t => t.NextActionOnUtc == t.DueOnUtc && t.BreachedOnUtc == null);
    }

    [Fact]
    public void Complete_Should_DequeueTimer_WhenCompletedUntilDeadline()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();

        // Act
        TicketSlaClock.Complete(resolution, SaoPaulo(12, 10), SlaCompletionCause.Resolution);

        // Assert
        resolution.NextActionOnUtc.ShouldBeNull();
    }

    [Fact]
    public void Complete_Should_KeepTimerQueued_WhenCompletedAfterDeadline()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();

        // Act
        TicketSlaClock.Complete(resolution, SaoPaulo(12, 30), SlaCompletionCause.Resolution);

        // Assert
        resolution.NextActionOnUtc.ShouldBe(resolution.DueOnUtc);
        resolution.BreachedOnUtc.ShouldBeNull();
    }

    [Fact]
    public void Stop_Should_DequeueTimer()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();

        // Act
        TicketSlaClock.Stop(resolution, SaoPaulo(13, 0));

        // Assert
        resolution.NextActionOnUtc.ShouldBeNull();
        resolution.BreachedOnUtc.ShouldBeNull();
    }

    [Fact]
    public void ProcessDue_Should_RecordBreachAtDeadlineAndRaiseEvent_WhenTimerIsOpen()
    {
        // Arrange
        TicketSlaTimer firstResponse = UrgentFirstResponse();

        // Act
        bool breached = TicketSlaClock.ProcessDue(firstResponse);

        // Assert
        breached.ShouldBeTrue();
        firstResponse.BreachedOnUtc.ShouldBe(SaoPaulo(8, 40));
        firstResponse.NextActionOnUtc.ShouldBeNull();
        firstResponse.DomainEvents.ShouldHaveSingleItem().ShouldBe(
            new TicketSlaBreachedDomainEvent(firstResponse.TicketId, SlaMetric.FirstResponse, 1, SaoPaulo(8, 40)));
    }

    [Fact]
    public void ProcessDue_Should_RecordBreach_WhenTimerWasCompletedAfterDeadline()
    {
        // Arrange
        TicketSlaTimer firstResponse = UrgentFirstResponse();
        TicketSlaClock.Complete(firstResponse, SaoPaulo(8, 45), SlaCompletionCause.Message);

        // Act
        bool breached = TicketSlaClock.ProcessDue(firstResponse);

        // Assert
        breached.ShouldBeTrue();
        firstResponse.BreachedOnUtc.ShouldBe(SaoPaulo(8, 40));
        firstResponse.CompletedOnUtc.ShouldBe(SaoPaulo(8, 45));
        firstResponse.DomainEvents.ShouldHaveSingleItem();
    }

    [Fact]
    public void ProcessDue_Should_OnlyDequeue_WhenTimerWasCompletedOnTimeOrStopped()
    {
        // Arrange
        TicketSlaTimer completedOnTime = UrgentFirstResponse();
        completedOnTime.CompletedOnUtc = SaoPaulo(8, 30);
        completedOnTime.NextActionOnUtc = completedOnTime.DueOnUtc;
        TicketSlaTimer stopped = UrgentResolution();
        stopped.StoppedOnUtc = SaoPaulo(9, 0);

        // Act
        bool completedBreached = TicketSlaClock.ProcessDue(completedOnTime);
        bool stoppedBreached = TicketSlaClock.ProcessDue(stopped);

        // Assert
        completedBreached.ShouldBeFalse();
        stoppedBreached.ShouldBeFalse();
        new[] { completedOnTime, stopped }.ShouldAllBe(t => t.BreachedOnUtc == null && t.NextActionOnUtc == null && !t.DomainEvents.Any());
    }

    [Fact]
    public void StartResolutionCycle_Should_QueueNewCycleAtItsDeadline()
    {
        // Act: reaberto às 14:00; 240 minutos úteis terminam às 18:00, na borda da faixa.
        TicketSlaTimer cycle = TicketSlaClock.StartResolutionCycle(Guid.NewGuid(), UrgentPlan, 2, Calendar, SaoPaulo(14, 0));

        // Assert
        cycle.NextActionOnUtc.ShouldBe(SaoPaulo(18, 0));
    }

    [Fact]
    public void Dequeue_Should_ClearNextActionWithoutBreachOrStop()
    {
        // Arrange
        TicketSlaTimer resolution = UrgentResolution();

        // Act
        TicketSlaClock.Dequeue(resolution);

        // Assert
        resolution.NextActionOnUtc.ShouldBeNull();
        resolution.BreachedOnUtc.ShouldBeNull();
        resolution.StoppedOnUtc.ShouldBeNull();
        resolution.DomainEvents.ShouldBeEmpty();
    }
}
