using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace TickestPristine.IntegrationTests.Evidence.Ct26;

[Collection(nameof(Ct26EvidenceCollection))]
public sealed class HybridCacheEvidenceTests(Ct26EvidenceFixture fixture, ITestOutputHelper output)
{
    private const string OldValue = "valor-antigo";
    private const string NewValue = "valor-novo";

    private static readonly TimeSpan Validity = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    private static readonly HybridCacheEntryOptions MemoryOnly = new() { Flags = HybridCacheEntryFlags.DisableDistributedCache };

    [EvidenceFact]
    public async Task GetOrCreateAsync_Should_ServeLateWriteUntilWritePlusValidity_WhenKeyIsRemovedDuringRead()
    {
        // Arrange: uma instância sem Redis, com a leitura presa por um portão
        await using CacheInstance instance = CreateInstance(redisConnectionString: null);
        string key = NewKey();
        var heldRead = new HeldRead(OldValue);
        var laterReads = new CountingRead(NewValue);

        long readStartedAt = Stopwatch.GetTimestamp();
        Task<string> heldCall = instance.Cache.GetOrCreateAsync(key, heldRead.ReadAsync).AsTask();
        await heldRead.Started.WaitAsync(WaitTimeout);

        // Act: a chave sai durante a leitura, que só termina depois de V
        await instance.Cache.RemoveAsync(key);
        await DelayUntilAsync(readStartedAt, Validity + Tolerance);
        heldRead.Release();
        string heldValue = await heldCall;
        long writtenAt = Stopwatch.GetTimestamp();

        List<CacheObservation> observations =
            await PollAsync(instance.Cache, key, laterReads, writtenAt, Validity + TimeSpan.FromSeconds(1));

        // Assert
        Record($"leitura presa por {Seconds(Stopwatch.GetElapsedTime(readStartedAt, writtenAt))} s; valor devolvido: {heldValue}");
        Record($"nova chamada da função em gravação + {Seconds(ElapsedSinceFirstCall(laterReads, writtenAt))} s");

        heldValue.ShouldBe(OldValue);
        ShouldHoldAround(
            observations,
            Validity,
            before: o => o.Value == OldValue && o.SourceCalls == 0,
            after: o => o.SourceCalls > 0);
    }

    [EvidenceFact]
    public async Task GetOrCreateAsync_Should_LetOtherInstanceUseRedisPayloadOnlyUntilReadStartPlusValidity()
    {
        // Arrange: duas instâncias com o mesmo Redis
        await using CacheInstance writer = CreateInstance(fixture.Redis.ConnectionString);
        await using CacheInstance reader = CreateInstance(fixture.Redis.ConnectionString);
        await using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(fixture.Redis.ConnectionString);
        IDatabase database = redis.GetDatabase();

        // Act: na primeira rodada, a leitura da A dura mais que V; na segunda, menos que V
        HeldWrite longRound = await WriteAfterHeldReadAsync(writer, Validity + TimeSpan.FromSeconds(1), database);
        var ownReadOnLongRound = new CountingRead(NewValue);
        string longRoundValue = await reader.Cache.GetOrCreateAsync(longRound.Key, ownReadOnLongRound.ReadAsync);

        HeldWrite shortRound = await WriteAfterHeldReadAsync(writer, Validity - TimeSpan.FromSeconds(1), database);
        var ownReadOnShortRound = new CountingRead(NewValue);
        List<CacheObservation> shortRoundObservations = await PollAsync(
            reader.Cache,
            shortRound.Key,
            ownReadOnShortRound,
            shortRound.ReadStartedAt,
            Validity + TimeSpan.FromSeconds(1));

        // Assert
        Record($"rodada longa: payload no Redis: {longRound.StoredInRedis}; a B devolveu {longRoundValue}, com {ownReadOnLongRound.Calls} chamada(s) da própria função");
        Record($"rodada curta: payload no Redis: {shortRound.StoredInRedis}; a B chamou a própria função em início + {Seconds(ElapsedSinceFirstCall(ownReadOnShortRound, shortRound.ReadStartedAt))} s");

        longRound.StoredInRedis.ShouldBeTrue();
        shortRound.StoredInRedis.ShouldBeTrue();
        longRoundValue.ShouldBe(NewValue);
        ownReadOnLongRound.Calls.ShouldBe(1);
        ShouldHoldAround(
            shortRoundObservations,
            Validity,
            before: o => o.Value == OldValue && o.SourceCalls == 0,
            after: o => o.SourceCalls > 0);
    }

    [EvidenceFact]
    public async Task GetOrCreateAsync_Should_StoreLateWriteInMemoryAndRedis_WhenKeyIsRemovedDuringRead()
    {
        // Arrange: uma leitura presa numa instância com Redis; outra instância, sem nada em memória, lê o Redis
        await using CacheInstance instance = CreateInstance(fixture.Redis.ConnectionString);
        await using CacheInstance redisProbe = CreateInstance(fixture.Redis.ConnectionString);
        await using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(fixture.Redis.ConnectionString);
        string key = NewKey();
        var heldRead = new HeldRead(OldValue);

        Task<string> heldCall = instance.Cache.GetOrCreateAsync(key, heldRead.ReadAsync).AsTask();
        await heldRead.Started.WaitAsync(WaitTimeout);

        // Act: a chave sai durante a leitura, e a leitura é liberada
        await instance.Cache.RemoveAsync(key);
        heldRead.Release();
        await heldCall;
        bool storedInRedis = await WaitForRedisKeyAsync(redis.GetDatabase(), key);

        var memoryRead = new CountingRead(NewValue);
        string fromMemory = await instance.Cache.GetOrCreateAsync(key, memoryRead.ReadAsync, MemoryOnly);
        var redisRead = new CountingRead(NewValue);
        string fromRedis = await redisProbe.Cache.GetOrCreateAsync(key, redisRead.ReadAsync);

        // Assert
        Record($"chave no Redis depois da gravação: {storedInRedis}; memória da instância: {fromMemory}; Redis: {fromRedis}");

        storedInRedis.ShouldBeTrue();
        fromMemory.ShouldBe(OldValue);
        memoryRead.Calls.ShouldBe(0);
        fromRedis.ShouldBe(OldValue);
        redisRead.Calls.ShouldBe(0);
    }

    [EvidenceFact]
    public async Task GetOrCreateAsync_Should_JoinHeldReadAndReturnOldValue_WhenSameKeyAndFlagsArriveAfterRemoval()
    {
        // Arrange: uma leitura presa, e a chave removida durante ela
        await using CacheInstance instance = CreateInstance(redisConnectionString: null);
        string key = NewKey();
        var heldRead = new HeldRead(OldValue);

        Task<string> heldCall = instance.Cache.GetOrCreateAsync(key, heldRead.ReadAsync).AsTask();
        await heldRead.Started.WaitAsync(WaitTimeout);
        await instance.Cache.RemoveAsync(key);

        // Act: outra chamada, com a mesma chave e as mesmas flags, chega depois da remoção
        var secondRead = new CountingRead(NewValue);
        Task<string> secondCall = instance.Cache.GetOrCreateAsync(key, secondRead.ReadAsync).AsTask();
        await Task.Delay(Tolerance);
        bool secondEndedBeforeRelease = secondCall.IsCompleted;

        heldRead.Release();
        string heldValue = await heldCall;
        string secondValue = await secondCall;

        // Assert
        Record($"a segunda chamada terminou antes da liberação: {secondEndedBeforeRelease}; valor: {secondValue}; chamadas da função dela: {secondRead.Calls}");

        secondEndedBeforeRelease.ShouldBeFalse();
        heldValue.ShouldBe(OldValue);
        secondValue.ShouldBe(OldValue);
        secondRead.Calls.ShouldBe(0);
    }

    [EvidenceFact]
    public async Task GetOrCreateAsync_Should_ServeValueWrittenAfterTagInvalidationUntilWritePlusValidity()
    {
        // Arrange: uma entrada com tag, com a leitura presa
        await using CacheInstance instance = CreateInstance(redisConnectionString: null);
        string key = NewKey();
        string[] tags = [$"tag-{Guid.NewGuid():N}"];
        var heldRead = new HeldRead(OldValue);
        var laterReads = new CountingRead(NewValue);

        Task<string> heldCall = instance.Cache.GetOrCreateAsync(key, heldRead.ReadAsync, tags: tags).AsTask();
        await heldRead.Started.WaitAsync(WaitTimeout);

        // Act: a tag é invalidada durante a leitura, que termina depois
        await instance.Cache.RemoveByTagAsync(tags[0]);
        await Task.Delay(TimeSpan.FromSeconds(1));
        heldRead.Release();
        string heldValue = await heldCall;
        long writtenAt = Stopwatch.GetTimestamp();

        List<CacheObservation> observations =
            await PollAsync(instance.Cache, key, laterReads, writtenAt, Validity + TimeSpan.FromSeconds(1), tags);

        // Assert
        Record($"valor devolvido depois da invalidação da tag: {heldValue}");
        Record($"nova chamada da função em gravação + {Seconds(ElapsedSinceFirstCall(laterReads, writtenAt))} s");

        heldValue.ShouldBe(OldValue);
        ShouldHoldAround(
            observations,
            Validity,
            before: o => o.Value == OldValue && o.SourceCalls == 0,
            after: o => o.SourceCalls > 0);
    }

    [EvidenceFact]
    public async Task GetOrCreateAsync_Should_ReadSourceAgainAtWritePlusValidityWithoutExtendingRedisTimeToLive()
    {
        // Arrange: uma entrada gravada com Redis; a cópia em memória vale 1 s, para que as leituras também passem pelo Redis
        await using CacheInstance instance = CreateInstance(fixture.Redis.ConnectionString, localCacheExpiration: TimeSpan.FromSeconds(1));
        await using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(fixture.Redis.ConnectionString);
        IDatabase database = redis.GetDatabase();
        string key = NewKey();
        var source = new CountingRead(OldValue);

        await instance.Cache.GetOrCreateAsync(key, source.ReadAsync);
        long writtenAt = Stopwatch.GetTimestamp();
        bool storedInRedis = await WaitForRedisKeyAsync(database, key);

        // Act: uma leitura a cada 0,5 s, sempre no meio de cada meio segundo
        var observations = new List<RedisObservation>();

        for (var offset = TimeSpan.FromMilliseconds(250);
             offset <= Validity + TimeSpan.FromSeconds(1);
             offset += TimeSpan.FromMilliseconds(500))
        {
            await DelayUntilAsync(writtenAt, offset);
            await instance.Cache.GetOrCreateAsync(key, source.ReadAsync);
            TimeSpan? timeToLive = await database.KeyTimeToLiveAsync(key);
            observations.Add(new RedisObservation(Stopwatch.GetElapsedTime(writtenAt), source.Calls, timeToLive));
        }

        // Assert
        TimeSpan? rereadAt = source.Calls > 1 ? Stopwatch.GetElapsedTime(writtenAt, source.CallTimestamps[1]) : null;
        List<TimeSpan?> timesToLive = [.. observations.Where(o => o.SourceCalls == 1).Select(o => o.TimeToLive)];

        Record($"releitura em gravação + {Seconds(rereadAt)} s");
        Record($"tempo de vida no Redis antes da releitura: {string.Join("; ", timesToLive.Select(Seconds))} s");

        storedInRedis.ShouldBeTrue();
        rereadAt.ShouldNotBeNull();
        rereadAt.Value.ShouldBeInRange(Validity - Tolerance, Validity + Tolerance);
        timesToLive.Count.ShouldBeGreaterThanOrEqualTo(3);
        timesToLive.ShouldAllBe(timeToLive => timeToLive != null);
        timesToLive.Zip(timesToLive.Skip(1)).ShouldAllBe(pair => pair.Second <= pair.First);
    }

    [EvidenceFact]
    public async Task GetOrCreateAsync_Should_AnticipateReaderDeadline_WhenReaderClockIsTwoSecondsAhead()
    {
        // Arrange: duas instâncias com o mesmo Redis; a A grava sempre com o relógio do sistema, e na segunda medição a B
        // lê com o relógio adiantado em 2 s, só na hora de parede
        var clockShift = TimeSpan.FromSeconds(2);
        var clockReadingMargin = TimeSpan.FromMilliseconds(50);

        // Act
        ReaderDeadline sameClocks = await MeasureReaderDeadlineAsync(TimeProvider.System);
        ReaderDeadline readerAhead = await MeasureReaderDeadlineAsync(new ShiftedSystemClock(clockShift));

        // Assert
        RecordDeadline("relógios iguais", sameClocks);
        RecordDeadline("relógio da B adiantado em 2 s", readerAhead);
        Record($"antecipação do prazo da B, em tempo real: {Seconds(sameClocks.Elapsed - readerAhead.Elapsed)} s");

        (sameClocks.ReaderClockAtWrite - sameClocks.WriterClockAtWrite).ShouldBeInRange(-clockReadingMargin, clockReadingMargin);
        (readerAhead.ReaderClockAtWrite - readerAhead.WriterClockAtWrite)
            .ShouldBeInRange(clockShift - clockReadingMargin, clockShift + clockReadingMargin);
        sameClocks.Elapsed.ShouldNotBeNull();
        readerAhead.Elapsed.ShouldNotBeNull();
        (sameClocks.Elapsed.Value - readerAhead.Elapsed.Value).ShouldBeInRange(clockShift - Tolerance, clockShift + Tolerance);
    }

    // Grava pela instância depois de uma leitura presa pelo tempo informado e espera a chave chegar ao Redis; devolve a
    // chave, o início da leitura e se a chave apareceu no Redis.
    private static async Task<HeldWrite> WriteAfterHeldReadAsync(CacheInstance writer, TimeSpan readDuration, IDatabase database)
    {
        string key = NewKey();
        var heldRead = new HeldRead(OldValue);

        long readStartedAt = Stopwatch.GetTimestamp();
        Task<string> call = writer.Cache.GetOrCreateAsync(key, heldRead.ReadAsync).AsTask();
        await heldRead.Started.WaitAsync(WaitTimeout);
        await DelayUntilAsync(readStartedAt, readDuration);
        heldRead.Release();
        await call;

        return new HeldWrite(key, readStartedAt, await WaitForRedisKeyAsync(database, key));
    }

    // A A grava com o relógio do sistema, e a B lê com o relógio informado. Mede, em tempo real (Stopwatch), quanto tempo
    // depois da gravação a B chama a própria função, e lê os relógios da A e da B na gravação e nessa chamada. O tempo fica
    // nulo se a chave não chegar ao Redis ou se a B não chamar a função dentro do prazo de observação.
    private async Task<ReaderDeadline> MeasureReaderDeadlineAsync(TimeProvider readerClock)
    {
        TimeProvider writerClock = TimeProvider.System;
        await using CacheInstance writer = CreateInstance(fixture.Redis.ConnectionString, clock: writerClock);
        await using CacheInstance reader = CreateInstance(fixture.Redis.ConnectionString, clock: readerClock);
        await using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(fixture.Redis.ConnectionString);
        string key = NewKey();

        await writer.Cache.GetOrCreateAsync(key, new CountingRead(OldValue).ReadAsync);
        long writtenAt = Stopwatch.GetTimestamp();
        DateTimeOffset writerClockAtWrite = writerClock.GetUtcNow();
        DateTimeOffset readerClockAtWrite = readerClock.GetUtcNow();

        long? ownReadAt = null;
        DateTimeOffset? writerClockAtOwnRead = null;
        DateTimeOffset? readerClockAtOwnRead = null;

        if (await WaitForRedisKeyAsync(redis.GetDatabase(), key))
        {
            while (ownReadAt is null && Stopwatch.GetElapsedTime(writtenAt) <= Validity * 3)
            {
                await reader.Cache.GetOrCreateAsync(key, OwnReadAsync);
                await Task.Delay(PollInterval);
            }
        }

        TimeSpan? elapsed = ownReadAt is { } calledAt ? Stopwatch.GetElapsedTime(writtenAt, calledAt) : null;

        return new ReaderDeadline(
            elapsed,
            writerClockAtWrite,
            readerClockAtWrite,
            writerClockAtOwnRead,
            readerClockAtOwnRead);

        // A função da B guarda o instante real e os dois relógios no momento em que é chamada.
        ValueTask<string> OwnReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ownReadAt ??= Stopwatch.GetTimestamp();
            writerClockAtOwnRead ??= writerClock.GetUtcNow();
            readerClockAtOwnRead ??= readerClock.GetUtcNow();

            return ValueTask.FromResult(NewValue);
        }
    }

    private void RecordDeadline(string round, ReaderDeadline deadline)
    {
        Record($"{round}: na gravação, relógio da A {Clock(deadline.WriterClockAtWrite)} e da B {Clock(deadline.ReaderClockAtWrite)} (B − A = {Seconds(deadline.ReaderClockAtWrite - deadline.WriterClockAtWrite)} s)");
        Record($"{round}: a B chamou a própria função {Seconds(deadline.Elapsed)} s depois da gravação, em tempo real; nesse instante, relógio da A {Clock(deadline.WriterClockAtOwnRead)} e da B {Clock(deadline.ReaderClockAtOwnRead)}");
    }

    private static async Task<List<CacheObservation>> PollAsync(
        HybridCache cache,
        string key,
        CountingRead source,
        long reference,
        TimeSpan duration,
        string[]? tags = null)
    {
        var observations = new List<CacheObservation>();

        while (Stopwatch.GetElapsedTime(reference) <= duration)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(reference);
            string value = await cache.GetOrCreateAsync(key, source.ReadAsync, tags: tags);
            observations.Add(new CacheObservation(elapsed, value, source.Calls));

            await Task.Delay(PollInterval);
        }

        return observations;
    }

    // Confere o comportamento antes e depois do limite, fora da faixa de tolerância em volta dele.
    private static void ShouldHoldAround(
        List<CacheObservation> observations,
        TimeSpan boundary,
        Expression<Func<CacheObservation, bool>> before,
        Expression<Func<CacheObservation, bool>> after)
    {
        List<CacheObservation> early = [.. observations.Where(o => o.Elapsed <= boundary - Tolerance)];
        List<CacheObservation> late = [.. observations.Where(o => o.Elapsed >= boundary + Tolerance)];

        early.ShouldNotBeEmpty();
        late.ShouldNotBeEmpty();
        early.ShouldAllBe(before);
        late.ShouldAllBe(after);
    }

    private static CacheInstance CreateInstance(
        string? redisConnectionString,
        TimeProvider? clock = null,
        TimeSpan? localCacheExpiration = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        if (clock is not null)
        {
            services.AddSingleton(clock);
        }

        if (redisConnectionString is not null)
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
        }

        services.AddHybridCache(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = Validity,
            LocalCacheExpiration = localCacheExpiration ?? Validity
        });

        return new CacheInstance(services.BuildServiceProvider());
    }

    private static async Task DelayUntilAsync(long reference, TimeSpan offset)
    {
        TimeSpan remaining = offset - Stopwatch.GetElapsedTime(reference);

        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }

    // A gravação no Redis pode terminar depois da resposta da instância: espera a chave aparecer, por até 1 s.
    private static async Task<bool> WaitForRedisKeyAsync(IDatabase database, string key)
    {
        long startedAt = Stopwatch.GetTimestamp();

        while (!await database.KeyExistsAsync(key))
        {
            if (Stopwatch.GetElapsedTime(startedAt) > TimeSpan.FromSeconds(1))
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        return true;
    }

    private static TimeSpan? ElapsedSinceFirstCall(CountingRead read, long reference) =>
        read.Calls > 0 ? Stopwatch.GetElapsedTime(reference, read.CallTimestamps[0]) : null;

    private static string NewKey() => $"evidencia-{Guid.NewGuid():N}";

    private static string Seconds(TimeSpan? value) =>
        value is null ? "nunca" : value.Value.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Clock(DateTimeOffset? value) =>
        value is null ? "nunca" : value.Value.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private void Record(FormattableString text) => output.WriteLine(text.ToString(CultureInfo.InvariantCulture));

    private sealed record CacheObservation(TimeSpan Elapsed, string Value, int SourceCalls);

    private sealed record RedisObservation(TimeSpan Elapsed, int SourceCalls, TimeSpan? TimeToLive);

    private sealed record HeldWrite(string Key, long ReadStartedAt, bool StoredInRedis);

    private sealed record ReaderDeadline(
        TimeSpan? Elapsed,
        DateTimeOffset WriterClockAtWrite,
        DateTimeOffset ReaderClockAtWrite,
        DateTimeOffset? WriterClockAtOwnRead,
        DateTimeOffset? ReaderClockAtOwnRead);

    private sealed class CacheInstance : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        public CacheInstance(ServiceProvider provider)
        {
            _provider = provider;
            Cache = provider.GetRequiredService<HybridCache>();
        }

        public HybridCache Cache { get; }

        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }

    // Função de leitura presa até o teste liberar; avisa quando começou.
    private sealed class HeldRead(string value)
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public void Release() => _released.TrySetResult();

        public async ValueTask<string> ReadAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);

            return value;
        }
    }

    // Função de leitura que responde na hora e guarda o instante de cada chamada.
    private sealed class CountingRead(string value)
    {
        private readonly ConcurrentQueue<long> _callTimestamps = new();

        public int Calls => _callTimestamps.Count;

        public IReadOnlyList<long> CallTimestamps => [.. _callTimestamps];

        public ValueTask<string> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _callTimestamps.Enqueue(Stopwatch.GetTimestamp());

            return ValueTask.FromResult(value);
        }
    }

    // Relógio do sistema com a hora de parede deslocada; o instante monotônico continua o real.
    private sealed class ShiftedSystemClock(TimeSpan shift) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + shift;
    }
}
