using System.Diagnostics.Metrics;

namespace TickestPristine.Application.UnitTests.Abstractions;

// Fábrica de Meter do teste, no lugar da do host: os Meter dela têm a própria fábrica como escopo, e o
// MeasurementRecorder escuta só esses, sem misturar as medidas de outros testes.
internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Tags = options.Tags, Scope = this });
        _meters.Add(meter);

        return meter;
    }

    public void Dispose()
    {
        foreach (Meter meter in _meters)
        {
            meter.Dispose();
        }

        _meters.Clear();
    }
}
