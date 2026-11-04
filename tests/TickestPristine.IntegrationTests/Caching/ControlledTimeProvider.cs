namespace TickestPristine.IntegrationTests.Caching;

/// <summary>
/// Relógio de teste: o instante monotônico só anda quando o teste chama <see cref="Advance"/> e nunca volta. Um tique do
/// instante é um tique de <see cref="TimeSpan"/>, então as idades medidas por <see cref="TimeProvider.GetElapsedTime(long)"/>
/// são exatas. A hora de parede acompanha o instante, deslocada pelo valor informado na criação.
/// </summary>
public sealed class ControlledTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset WallClockOrigin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly long _startTimestamp;
    private readonly TimeSpan _wallClockOffset;
    private long _timestamp;

    public ControlledTimeProvider(long startTimestamp = 0, TimeSpan wallClockOffset = default)
    {
        _startTimestamp = startTimestamp;
        _timestamp = startTimestamp;
        _wallClockOffset = wallClockOffset;
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

    public override DateTimeOffset GetUtcNow() =>
        WallClockOrigin + _wallClockOffset + TimeSpan.FromTicks(GetTimestamp() - _startTimestamp);

    /// <summary>
    /// Avança o instante monotônico; recusa um avanço negativo, porque o relógio nunca volta.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        Interlocked.Add(ref _timestamp, duration.Ticks);
    }
}
