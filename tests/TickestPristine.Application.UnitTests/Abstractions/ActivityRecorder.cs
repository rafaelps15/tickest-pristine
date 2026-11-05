using System.Collections.Concurrent;
using System.Diagnostics;
using TickestPristine.Application.Abstractions.Telemetry;

namespace TickestPristine.Application.UnitTests.Abstractions;

// Escuta os spans da fonte da Application. A fonte é do processo, então o teste filtra pelo nome dos próprios
// commands e queries.
internal sealed class ActivityRecorder : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _stopped = new();

    public ActivityRecorder()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ApplicationTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _stopped.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public Activity[] Named(string displayName) => [.. _stopped.Where(activity => activity.DisplayName == displayName)];

    public void Dispose() => _listener.Dispose();
}
