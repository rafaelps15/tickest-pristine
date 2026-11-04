using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace TickestPristine.IntegrationTests.Caching.StampAge;

/// <summary>
/// Protótipo, só do projeto de testes, da conferência do carimbo de segurança com validade curta medida pelo relógio
/// monotônico da própria instância. O carimbo lido fica num dicionário privado desta instância, com o instante em que a
/// leitura começou e o identificador da instância; nada passa por cache da aplicação nem por cache distribuído, e nenhum
/// método devolve carimbo ou entrada.
/// </summary>
public sealed class StampAgeCachePrototype
{
    private static readonly AgeSteps StoredEntrySteps = new(
        StampAgeStepKind.StoredAgeMeasured,
        StampAgeStepKind.StoredAgeNegative,
        StampAgeStepKind.StoredAgeNotNegative,
        StampAgeStepKind.StoredAgeExpired,
        StampAgeStepKind.StoredAgeWithinValidity);

    private static readonly AgeSteps FreshReadSteps = new(
        StampAgeStepKind.FreshAgeMeasured,
        StampAgeStepKind.FreshAgeNegative,
        StampAgeStepKind.FreshAgeNotNegative,
        StampAgeStepKind.FreshAgeExpired,
        StampAgeStepKind.FreshAgeWithinValidity);

    private readonly ConcurrentDictionary<Guid, StampAgeEntry> _entries = new();
    private readonly ConcurrentQueue<StampAgeStep> _steps = new();
    private readonly TimeProvider _clock;
    private readonly Func<Guid, CancellationToken, Task<Guid>> _readStamp;
    private readonly TimeSpan _validity;
    private long _lastDecisionId;
    private int _sourceReads;

    public StampAgeCachePrototype(TimeProvider clock, Func<Guid, CancellationToken, Task<Guid>> readStamp, TimeSpan validity)
    {
        _clock = clock;
        _readStamp = readStamp;
        _validity = validity;
    }

    public Guid InstanceId { get; } = Guid.NewGuid();

    public int SourceReadCount => Volatile.Read(ref _sourceReads);

    public IReadOnlyList<StampAgeStep> Steps => [.. _steps];

    /// <summary>
    /// Decide se o carimbo do token vale para o usuário. Antes de qualquer decisão, confere o valor do dicionário nesta
    /// ordem: ele existe; é desta instância; a idade, desde o início da leitura que o produziu, é medida pelo relógio desta
    /// instância; ela não é negativa; e é menor que a validade. Se alguma conferência falhar, lê a fonte, sem esperar
    /// outra leitura em andamento, e confere a idade do resultado antes de gravá-lo ou usá-lo: se ela não estiver em
    /// <c>0 ≤ idade &lt; validade</c>, recusa a decisão sem gravar e sem repetir a leitura.
    /// </summary>
    public async Task<StampAgeDecision> DecideAsync(Guid userId, Guid tokenStamp, CancellationToken cancellationToken = default)
    {
        long decisionId = Interlocked.Increment(ref _lastDecisionId);

        if (TryGetValidEntry(decisionId, userId, out StampAgeEntry? entry))
        {
            return Decide(decisionId, entry.Stamp == tokenStamp);
        }

        long readStartedAt = _clock.GetTimestamp();
        Log(decisionId, StampAgeStepKind.SourceReadStarted);
        Interlocked.Increment(ref _sourceReads);

        Guid stamp = await _readStamp(userId, cancellationToken);
        Log(decisionId, StampAgeStepKind.SourceReadCompleted);

        if (!HasValidAge(decisionId, readStartedAt, FreshReadSteps))
        {
            return Finish(decisionId, StampAgeOutcome.FailedClosed);
        }

        // Gravação sem condição: uma leitura antiga que termina depois sobrescreve a nova, mas a idade do valor dela
        // continua contada do início dela.
        _entries[userId] = new StampAgeEntry(stamp, readStartedAt, InstanceId);
        Log(decisionId, StampAgeStepKind.FreshResultStored);

        return Decide(decisionId, stamp == tokenStamp);
    }

    /// <summary>
    /// Remove a entrada do usuário do dicionário desta instância. Não renova a validade de nenhum valor: uma leitura
    /// iniciada antes e gravada depois continua com a idade contada do início dela.
    /// </summary>
    public void Invalidate(Guid userId) => _entries.TryRemove(userId, out _);

    /// <summary>
    /// Só para os testes: grava no dicionário uma entrada montada pelo teste, sem conferir nada, para mostrar como a
    /// decisão trata um valor de outra instância ou com a idade fora da validade.
    /// </summary>
    public void PlantForTest(Guid userId, StampAgeEntry entry) => _entries[userId] = entry;

    // Conferências do valor do dicionário, na ordem, até a primeira que falhar.
    private bool TryGetValidEntry(long decisionId, Guid userId, [NotNullWhen(true)] out StampAgeEntry? entry)
    {
        if (!_entries.TryGetValue(userId, out entry))
        {
            Log(decisionId, StampAgeStepKind.EntryAbsent);
            return false;
        }

        Log(decisionId, StampAgeStepKind.EntryPresent);

        if (entry.InstanceId != InstanceId)
        {
            Log(decisionId, StampAgeStepKind.InstanceDiffers);
            return false;
        }

        Log(decisionId, StampAgeStepKind.InstanceMatches);

        return HasValidAge(decisionId, entry.ReadStartedAt, StoredEntrySteps);
    }

    // A idade, desde o início da leitura e pelo relógio desta instância, precisa estar em 0 ≤ idade < validade.
    private bool HasValidAge(long decisionId, long readStartedAt, AgeSteps steps)
    {
        TimeSpan age = _clock.GetElapsedTime(readStartedAt);
        Log(decisionId, steps.Measured, age);

        if (age < TimeSpan.Zero)
        {
            Log(decisionId, steps.Negative);
            return false;
        }

        Log(decisionId, steps.NotNegative);

        if (age >= _validity)
        {
            Log(decisionId, steps.Expired);
            return false;
        }

        Log(decisionId, steps.WithinValidity);

        return true;
    }

    private StampAgeDecision Decide(long decisionId, bool stampMatches) =>
        Finish(decisionId, stampMatches ? StampAgeOutcome.Accepted : StampAgeOutcome.Rejected);

    private StampAgeDecision Finish(long decisionId, StampAgeOutcome outcome)
    {
        Log(decisionId, outcome switch
        {
            StampAgeOutcome.Accepted => StampAgeStepKind.Accepted,
            StampAgeOutcome.Rejected => StampAgeStepKind.Rejected,
            _ => StampAgeStepKind.FailedClosed
        });

        return new StampAgeDecision(decisionId, outcome);
    }

    private void Log(long decisionId, StampAgeStepKind kind, TimeSpan? age = null) =>
        _steps.Enqueue(new StampAgeStep(decisionId, kind, age));

    private sealed record AgeSteps(
        StampAgeStepKind Measured,
        StampAgeStepKind Negative,
        StampAgeStepKind NotNegative,
        StampAgeStepKind Expired,
        StampAgeStepKind WithinValidity);
}

/// <summary>
/// Valor guardado pelo protótipo: o carimbo, o instante do relógio monotônico da instância em que a leitura começou e o
/// identificador da instância que fez a leitura.
/// </summary>
public sealed record StampAgeEntry(Guid Stamp, long ReadStartedAt, Guid InstanceId);

public sealed record StampAgeDecision(long Id, StampAgeOutcome Outcome);

public sealed record StampAgeStep(long DecisionId, StampAgeStepKind Kind, TimeSpan? Age);

public enum StampAgeOutcome
{
    Accepted,
    Rejected,
    FailedClosed
}

public enum StampAgeStepKind
{
    EntryAbsent,
    EntryPresent,
    InstanceDiffers,
    InstanceMatches,
    StoredAgeMeasured,
    StoredAgeNegative,
    StoredAgeNotNegative,
    StoredAgeExpired,
    StoredAgeWithinValidity,
    SourceReadStarted,
    SourceReadCompleted,
    FreshAgeMeasured,
    FreshAgeNegative,
    FreshAgeNotNegative,
    FreshAgeExpired,
    FreshAgeWithinValidity,
    FreshResultStored,
    Accepted,
    Rejected,
    FailedClosed
}
