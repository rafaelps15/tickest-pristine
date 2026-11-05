using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace TickestPristine.Tests.Shared.Abstractions;

// Escuta as medidas dos Meter de uma fábrica (o escopo deles) para o teste somar o que cada contador registrou; com a
// fábrica do teste ou do host do teste, as medidas de outros testes e de outros hosts ficam de fora.
public sealed class MeasurementRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<RecordedMeasurement> _measurements = new();

    public MeasurementRecorder(IMeterFactory scope)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, scope))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _measurements.Enqueue(new RecordedMeasurement(instrument.Name, value, tags.ToArray())));
        _listener.Start();
    }

    public long Sum(string instrument, string? tagKey = null, object? tagValue = null) =>
        _measurements
            .Where(measurement => measurement.Instrument == instrument &&
                (tagKey is null || measurement.Tags.Any(tag => tag.Key == tagKey && Equals(tag.Value, tagValue))))
            .Sum(measurement => measurement.Value);

    public void Dispose() => _listener.Dispose();

    private sealed record RecordedMeasurement(string Instrument, long Value, KeyValuePair<string, object?>[] Tags);
}
