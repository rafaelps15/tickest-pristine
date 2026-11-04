using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Web.Api;
using static TickestPristine.IntegrationTests.Caching.StampAge.StampAgeStepKind;

namespace TickestPristine.IntegrationTests.Caching.StampAge;

[Collection(nameof(StampAgePrototypeCollection))]
public sealed class StampAgeCachePrototypeTests(IntegrationTestWebAppFactory factory, RedisContainerFixture redis)
{
    private const string UserPassword = "Password123!";
    private const string NewUserPassword = "Password456!";
    private const string InstanceA = "A";
    private const string InstanceB = "B";

    private static readonly TimeSpan Validity = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OneTick = TimeSpan.FromTicks(1);
    private static readonly TimeSpan HalfSecond = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    // Instantes das decisões com o carimbo antigo depois que a leitura antiga grava, contados do início dela.
    private static readonly TimeSpan[] OffsetsAfterOldWrite =
    [
        TimeSpan.FromSeconds(1.5),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2.5),
        Validity - OneTick,
        Validity,
        Validity + HalfSecond
    ];

    // Conferências do valor do dicionário quando ele vale e quando a idade chegou à validade.
    private static readonly StampAgeStepKind[] StoredEntryValid =
        [EntryPresent, InstanceMatches, StoredAgeMeasured, StoredAgeNotNegative, StoredAgeWithinValidity];

    private static readonly StampAgeStepKind[] StoredEntryExpired =
        [EntryPresent, InstanceMatches, StoredAgeMeasured, StoredAgeNotNegative, StoredAgeExpired];

    // Leitura nova que passa pelas conferências de idade e é gravada.
    private static readonly StampAgeStepKind[] FreshReadStored =
    [
        SourceReadStarted,
        SourceReadCompleted,
        FreshAgeMeasured,
        FreshAgeNotNegative,
        FreshAgeWithinValidity,
        FreshResultStored
    ];

    // O resto é dado ou leitura e gravação de chaves.
    private static readonly HashSet<string> RedisConnectionCommands = new(StringComparer.Ordinal)
    {
        "auth", "client", "cluster", "command", "config", "echo", "hello", "info", "ping", "psubscribe",
        "punsubscribe", "quit", "readonly", "readwrite", "reset", "role", "select", "sentinel", "ssubscribe",
        "subscribe", "sunsubscribe", "time", "unsubscribe"
    };

    private static readonly string[] ForbiddenPrototypeReferences =
    [
        "HybridCache", "MemoryCache", "IDistributedCache", "StackExchange.Redis", "IConnectionMultiplexer",
        "IServiceProvider", "System.Reflection", "GetType(", "typeof(", "AddOrUpdate", "GetOrAdd", "TryAdd",
        "TryUpdate"
    ];

    public enum EntrySituation
    {
        Absent,
        FromAnotherInstance,
        NegativeAge,
        AgeEqualToValidity,
        Valid
    }

    [Fact]
    public void PrototypeSource_Should_TakeOnlyClockSourceAndValidityAndReferenceNoCache()
    {
        // Arrange
        string source = File.ReadAllText(PrototypeSourcePath());

        // Act
        string[] forbiddenFound = [.. ForbiddenPrototypeReferences.Where(reference => source.Contains(reference, StringComparison.Ordinal))];
        int constructors = CountOccurrences(source, "StampAgeCachePrototype(");
        List<string> publicMembers = PublicMemberDeclarations(source);

        // Assert
        forbiddenFound.ShouldBeEmpty();
        constructors.ShouldBe(1);
        source.ShouldContain(
            "public StampAgeCachePrototype(TimeProvider clock, Func<Guid, CancellationToken, Task<Guid>> readStamp, TimeSpan validity)");

        // As únicas operações: a decisão, a invalidação, o identificador, o método de teste e os diagnósticos.
        publicMembers.ShouldBe(
        [
            "StampAgeCachePrototype",
            "Guid InstanceId",
            "int SourceReadCount",
            "IReadOnlyList<StampAgeStep> Steps",
            "async Task<StampAgeDecision> DecideAsync",
            "void Invalidate",
            "void PlantForTest"
        ]);

        // A decisão e o registro de passos não carregam carimbo.
        source.ShouldContain("public sealed record StampAgeDecision(long Id, StampAgeOutcome Outcome);");
        source.ShouldContain("public sealed record StampAgeStep(long DecisionId, StampAgeStepKind Kind, TimeSpan? Age);");
    }

    [Fact]
    public async Task DecideAsync_Should_LeaveApplicationMemoryAndRedisUntouched_WhenHostedBesideProductionCaches()
    {
        // Arrange: protótipos construídos só com relógio, fonte e validade, registrados num host igual ao de produção,
        // com o HybridCache da aplicação e o Redis isolado desta coleção
        var source = new ControlledStampSource();
        var userId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        var plantedStamp = Guid.NewGuid();
        source.SetStamp(userId, stamp);

        var prototypeA = new StampAgeCachePrototype(new ControlledTimeProvider(), source.ReadAsync, Validity);
        var prototypeB = new StampAgeCachePrototype(new ControlledTimeProvider(7_000_000), source.ReadAsync, Validity);

        await using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Cache", redis.ConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddKeyedSingleton(InstanceA, prototypeA);
                services.AddKeyedSingleton(InstanceB, prototypeB);
            });
        });

        StampAgeCachePrototype hostedA = host.Services.GetRequiredKeyedService<StampAgeCachePrototype>(InstanceA);
        StampAgeCachePrototype hostedB = host.Services.GetRequiredKeyedService<StampAgeCachePrototype>(InstanceB);
        HybridCache applicationCache = host.Services.GetRequiredService<HybridCache>();
        var applicationMemory = (MemoryCache)host.Services.GetRequiredService<IMemoryCache>();

        await using ConnectionMultiplexer redisConnection =
            await ConnectionMultiplexer.ConnectAsync($"{redis.ConnectionString},allowAdmin=true");
        IServer redisServer = redisConnection.GetServer(redisConnection.GetEndPoints()[0]);
        IDatabase redisDatabase = redisConnection.GetDatabase();

        // O cache da aplicação já fala com o Redis isolado antes da medição.
        string warmUpKey = $"aquecimento-{Guid.NewGuid():N}";
        await applicationCache.SetAsync(warmUpKey, "aquecimento");
        bool applicationCacheUsesRedis = await WaitForRedisKeyAsync(redisDatabase, warmUpKey);

        Dictionary<string, long> commandsBefore = await StableDataCommandCallsAsync(redisServer);
        int memoryCountBefore = applicationMemory.Count;

        // Act: o protótipo lê, decide e invalida o carimbo do usuário
        StampAgeDecision firstRead = await hostedA.DecideAsync(userId, stamp);
        StampAgeDecision fromEntry = await hostedA.DecideAsync(userId, stamp);
        hostedA.Invalidate(userId);
        StampAgeDecision afterInvalidation = await hostedA.DecideAsync(userId, stamp);
        StampAgeDecision onOtherInstance = await hostedB.DecideAsync(userId, stamp);

        Dictionary<string, long> commandsAfter = await DataCommandCallsAsync(redisServer);
        int memoryCountAfter = applicationMemory.Count;
        List<string> userKeysInRedis = await KeysOfAsync(redisServer, userId);

        // Depois, valores do mesmo usuário são plantados no Redis isolado e na memória padrão da aplicação.
        await applicationCache.SetAsync($"security-stamp-{userId}", plantedStamp);
        await redisDatabase.StringSetAsync($"stamp:{userId}", plantedStamp.ToString());
        applicationMemory.Set($"user-stamp-{userId}", plantedStamp);
        hostedA.Invalidate(userId);
        hostedB.Invalidate(userId);

        int readsOnABeforePlanted = hostedA.SourceReadCount;
        int readsOnBBeforePlanted = hostedB.SourceReadCount;
        Dictionary<string, long> commandsBeforePlanted = await StableDataCommandCallsAsync(redisServer);
        int memoryCountBeforePlanted = applicationMemory.Count;

        StampAgeDecision plantedOnA = await hostedA.DecideAsync(userId, plantedStamp);
        StampAgeDecision plantedOnB = await hostedB.DecideAsync(userId, plantedStamp);

        Dictionary<string, long> commandsAfterPlanted = await DataCommandCallsAsync(redisServer);
        int memoryCountAfterPlanted = applicationMemory.Count;

        // Assert
        applicationCacheUsesRedis.ShouldBeTrue();
        hostedA.ShouldBeSameAs(prototypeA);
        hostedB.ShouldBeSameAs(prototypeB);
        firstRead.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        fromEntry.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        afterInvalidation.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        onOtherInstance.Outcome.ShouldBe(StampAgeOutcome.Accepted);

        IncreasedCommands(commandsBefore, commandsAfter).ShouldBeEmpty();
        memoryCountAfter.ShouldBe(memoryCountBefore);
        userKeysInRedis.ShouldBeEmpty();

        plantedOnA.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        plantedOnB.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        (hostedA.SourceReadCount - readsOnABeforePlanted).ShouldBe(1);
        (hostedB.SourceReadCount - readsOnBBeforePlanted).ShouldBe(1);
        IncreasedCommands(commandsBeforePlanted, commandsAfterPlanted).ShouldBeEmpty();
        memoryCountAfterPlanted.ShouldBe(memoryCountBeforePlanted);
    }

    [Fact]
    public async Task DecideAsync_Should_RefuseEntryFromAnotherInstanceBeforeMeasuringAnyAge()
    {
        // Arrange: A e B com identificador e relógio próprios; na B, uma entrada com o identificador da A e idade de 1 s
        // no relógio da B, menor que a validade
        var source = new ControlledStampSource();
        var userId = Guid.NewGuid();
        var currentStamp = Guid.NewGuid();
        var plantedStamp = Guid.NewGuid();
        source.SetStamp(userId, currentStamp);

        var clockB = new ControlledTimeProvider(90_000_000);
        var instanceA = new StampAgeCachePrototype(new ControlledTimeProvider(50_000_000), source.ReadAsync, Validity);
        var instanceB = new StampAgeCachePrototype(clockB, source.ReadAsync, Validity);
        instanceB.PlantForTest(userId, new StampAgeEntry(plantedStamp, clockB.GetTimestamp() - OneSecond.Ticks, instanceA.InstanceId));

        // Act
        StampAgeDecision withPlantedStamp = await instanceB.DecideAsync(userId, plantedStamp);
        StampAgeDecision withReadStamp = await instanceB.DecideAsync(userId, currentStamp);

        // Assert
        withPlantedStamp.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(instanceB, withPlantedStamp).ShouldBe([EntryPresent, InstanceDiffers, .. FreshReadStored, Rejected]);
        withReadStamp.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        KindsOf(instanceB, withReadStamp).ShouldBe([.. StoredEntryValid, Accepted]);
        instanceB.SourceReadCount.ShouldBe(1);
        instanceA.SourceReadCount.ShouldBe(0);
    }

    [Fact]
    public async Task DecideAsync_Should_UseEntryUntilOneTickBeforeValidityAndReadSourceOnceAtValidity()
    {
        // Arrange: uma entrada da própria instância, com a leitura iniciada em t
        (_, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid stamp, _) = CreateScenario();
        await prototype.DecideAsync(userId, stamp);

        // Act: decisões em t + V − 1 tique, em t + V e em t + V + 1 s
        clock.Advance(Validity - OneTick);
        StampAgeDecision atLastTick = await prototype.DecideAsync(userId, stamp);
        int readsAtLastTick = prototype.SourceReadCount;

        clock.Advance(OneTick);
        StampAgeDecision atValidity = await prototype.DecideAsync(userId, stamp);
        int readsAtValidity = prototype.SourceReadCount;

        clock.Advance(OneSecond);
        StampAgeDecision oneSecondLater = await prototype.DecideAsync(userId, stamp);
        int readsOneSecondLater = prototype.SourceReadCount;

        // Assert
        atLastTick.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        readsAtLastTick.ShouldBe(1);
        KindsOf(prototype, atLastTick).ShouldBe([.. StoredEntryValid, Accepted]);
        AgeOf(prototype, atLastTick, StoredAgeMeasured).ShouldBe(Validity - OneTick);

        atValidity.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        readsAtValidity.ShouldBe(2);
        KindsOf(prototype, atValidity).ShouldBe([.. StoredEntryExpired, .. FreshReadStored, Accepted]);
        AgeOf(prototype, atValidity, FreshAgeMeasured).ShouldBe(TimeSpan.Zero);

        oneSecondLater.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        readsOneSecondLater.ShouldBe(2);
        AgeOf(prototype, oneSecondLater, StoredAgeMeasured).ShouldBe(OneSecond);
    }

    [Fact]
    public async Task DecideAsync_Should_ReadSourceItselfWithoutWaiting_WhenAnotherReadIsHeld()
    {
        // Arrange: uma leitura presa por um portão, iniciada antes da troca do carimbo
        (ControlledStampSource source, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid oldStamp, Guid newStamp) =
            CreateScenario();
        StampReadGate heldReadGate = source.HoldNextRead();
        Task<StampAgeDecision> heldDecision = prototype.DecideAsync(userId, oldStamp);
        await heldReadGate.Started.WaitAsync(WaitTimeout);

        clock.Advance(HalfSecond);
        source.SetStamp(userId, newStamp);

        // Act: outra requisição decide depois da troca
        StampAgeDecision withNewStamp = await prototype.DecideAsync(userId, newStamp);
        bool heldEndedBefore = heldDecision.IsCompleted;
        int readsWhileHeld = prototype.SourceReadCount;
        StampAgeDecision withOldStamp = await prototype.DecideAsync(userId, oldStamp);

        heldReadGate.Release();
        await heldDecision;

        // Assert
        heldEndedBefore.ShouldBeFalse();
        readsWhileHeld.ShouldBe(2);
        withNewStamp.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        KindsOf(prototype, withNewStamp).ShouldBe([EntryAbsent, .. FreshReadStored, Accepted]);
        withOldStamp.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(prototype, withOldStamp).ShouldBe([.. StoredEntryValid, Rejected]);
    }

    [Theory]
    [InlineData(EntrySituation.Absent)]
    [InlineData(EntrySituation.FromAnotherInstance)]
    [InlineData(EntrySituation.NegativeAge)]
    [InlineData(EntrySituation.AgeEqualToValidity)]
    [InlineData(EntrySituation.Valid)]
    public async Task DecideAsync_Should_FinishFiveChecksBeforeDeciding(EntrySituation situation)
    {
        // Arrange: no dicionário, nada ou uma entrada com o carimbo plantado, diferente do carimbo da fonte
        (_, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, _, _) = CreateScenario();
        var otherInstance = new StampAgeCachePrototype(new ControlledTimeProvider(), new ControlledStampSource().ReadAsync, Validity);
        var plantedStamp = Guid.NewGuid();
        long now = clock.GetTimestamp();

        StampAgeEntry? planted = situation switch
        {
            EntrySituation.FromAnotherInstance => new StampAgeEntry(plantedStamp, now - OneSecond.Ticks, otherInstance.InstanceId),
            EntrySituation.NegativeAge => new StampAgeEntry(plantedStamp, now + OneTick.Ticks, prototype.InstanceId),
            EntrySituation.AgeEqualToValidity => new StampAgeEntry(plantedStamp, now - Validity.Ticks, prototype.InstanceId),
            EntrySituation.Valid => new StampAgeEntry(plantedStamp, now - OneSecond.Ticks, prototype.InstanceId),
            _ => null
        };

        if (planted is not null)
        {
            prototype.PlantForTest(userId, planted);
        }

        // Act
        StampAgeDecision decision = await prototype.DecideAsync(userId, plantedStamp);

        // Assert
        KindsOf(prototype, decision).ShouldBe(ExpectedSteps(situation));
        prototype.SourceReadCount.ShouldBe(situation == EntrySituation.Valid ? 0 : 1);
        decision.Outcome.ShouldBe(situation == EntrySituation.Valid ? StampAgeOutcome.Accepted : StampAgeOutcome.Rejected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DecideAsync_Should_AcceptOldStampAtMostUntilOldReadStartPlusValidity(int instanceCount)
    {
        // Arrange: em cada instância, a leitura mais antiga em andamento começa em t0, antes da troca do carimbo
        var source = new ControlledStampSource();
        var userId = Guid.NewGuid();
        var oldStamp = Guid.NewGuid();
        var newStamp = Guid.NewGuid();
        source.SetStamp(userId, oldStamp);

        (StampAgeCachePrototype Prototype, ControlledTimeProvider Clock)[] instances =
        [
            .. Enumerable.Range(0, instanceCount).Select(index => CreateInstance(source, 1_000_000_000 + index * 333_333))
        ];

        // Act
        List<TimedDecision>[] decisions = await RunStampChangeAsync(source, userId, oldStamp, newStamp, instances);

        // Assert
        decisions.Length.ShouldBe(instanceCount);

        foreach (List<TimedDecision> instanceDecisions in decisions)
        {
            ShouldAcceptOldStampOnlyBeforeValidity(instanceDecisions);
        }
    }

    [Fact]
    public async Task DecideAsync_Should_ReadSourceBeforeDeciding_WhenEntryFromAnotherRequestIsExactlyValidityOld()
    {
        // Arrange: uma entrada gravada por outra requisição da mesma instância; o carimbo troca e a idade chega a V
        (ControlledStampSource source, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid oldStamp, Guid newStamp) =
            CreateScenario();
        StampAgeDecision otherRequest = await prototype.DecideAsync(userId, oldStamp);
        source.SetStamp(userId, newStamp);
        clock.Advance(Validity);
        int readsBefore = prototype.SourceReadCount;

        // Act
        StampAgeDecision decision = await prototype.DecideAsync(userId, oldStamp);

        // Assert
        otherRequest.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        (prototype.SourceReadCount - readsBefore).ShouldBe(1);
        decision.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(prototype, decision).ShouldBe([.. StoredEntryExpired, .. FreshReadStored, Rejected]);
        AgeOf(prototype, decision, StoredAgeMeasured).ShouldBe(Validity);
    }

    [Fact]
    public async Task DecideAsync_Should_ReadSourceExactlyAtFirstDecisionWithAgeAtValidity_WhenDecidingEveryHalfSecond()
    {
        // Arrange: uma entrada lida em t
        (_, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid stamp, _) = CreateScenario();
        await prototype.DecideAsync(userId, stamp);

        // Act: uma decisão a cada 0,5 s de tempo controlado, até a idade chegar a V
        var decisions = new List<StampAgeDecision>();
        var readsAfterEach = new List<int>();

        for (int step = 1; step <= 6; step++)
        {
            clock.Advance(HalfSecond);
            decisions.Add(await prototype.DecideAsync(userId, stamp));
            readsAfterEach.Add(prototype.SourceReadCount);
        }

        // Assert
        readsAfterEach.ShouldBe([1, 1, 1, 1, 1, 2]);
        decisions.Select(decision => AgeOf(prototype, decision, StoredAgeMeasured)).ShouldBe(
        [
            TimeSpan.FromSeconds(0.5),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1.5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2.5),
            Validity
        ]);
        decisions.Take(5).Select(decision => KindsOf(prototype, decision)).ShouldAllBe(kinds => kinds.SequenceEqual(new[] { EntryPresent, InstanceMatches, StoredAgeMeasured, StoredAgeNotNegative, StoredAgeWithinValidity, Accepted }));
        KindsOf(prototype, decisions[^1]).ShouldBe([.. StoredEntryExpired, .. FreshReadStored, Accepted]);
    }

    [Fact]
    public async Task DecideAsync_Should_KeepEachInstanceWindow_WhenClocksStartDifferentlyAndWallTimesAreShifted()
    {
        // Arrange: duas instâncias com relógios controlados independentes, pontos de partida diferentes e hora de parede
        // deslocada em mais e menos 5 s
        var source = new ControlledStampSource();
        var userId = Guid.NewGuid();
        var oldStamp = Guid.NewGuid();
        var newStamp = Guid.NewGuid();
        source.SetStamp(userId, oldStamp);

        var clockA = new ControlledTimeProvider(1_000_000, TimeSpan.FromSeconds(5));
        var clockB = new ControlledTimeProvider(987_654_321, TimeSpan.FromSeconds(-5));
        var instanceA = new StampAgeCachePrototype(clockA, source.ReadAsync, Validity);
        var instanceB = new StampAgeCachePrototype(clockB, source.ReadAsync, Validity);

        // Act: a sequência da troca do carimbo roda nas duas; depois, a B recebe um valor medido pelo relógio da A
        List<TimedDecision>[] decisions =
            await RunStampChangeAsync(source, userId, oldStamp, newStamp, (instanceA, clockA), (instanceB, clockB));
        TimeSpan wallClockDifference = clockA.GetUtcNow() - clockB.GetUtcNow();

        instanceB.PlantForTest(userId, new StampAgeEntry(oldStamp, clockA.GetTimestamp(), instanceA.InstanceId));
        StampAgeDecision withValueMeasuredByA = await instanceB.DecideAsync(userId, oldStamp);

        // Assert
        wallClockDifference.ShouldBe(TimeSpan.FromSeconds(10));
        ShouldAcceptOldStampOnlyBeforeValidity(decisions[0]);
        ShouldAcceptOldStampOnlyBeforeValidity(decisions[1]);
        decisions[0].Select(decision => decision.SinceOldReadStart).ShouldBe(decisions[1].Select(decision => decision.SinceOldReadStart));
        withValueMeasuredByA.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(instanceB, withValueMeasuredByA).ShouldBe([EntryPresent, InstanceDiffers, .. FreshReadStored, Rejected]);
    }

    [Fact]
    public async Task Invalidate_Should_MakeNextDecisionReadSourceAndRejectOldStamp_WhenStampChanges()
    {
        // Arrange: uma entrada válida com o carimbo antigo, e a troca do carimbo
        (ControlledStampSource source, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid oldStamp, Guid newStamp) =
            CreateScenario();
        await prototype.DecideAsync(userId, oldStamp);
        clock.Advance(OneSecond);
        source.SetStamp(userId, newStamp);

        // Act: o protótipo invalida a entrada, e a próxima decisão acontece na mesma instância
        prototype.Invalidate(userId);
        StampAgeDecision decision = await prototype.DecideAsync(userId, oldStamp);

        // Assert
        decision.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(prototype, decision).ShouldBe([EntryAbsent, .. FreshReadStored, Rejected]);
        prototype.SourceReadCount.ShouldBe(2);
    }

    [Fact]
    public async Task Invalidate_Should_NotRenewLateWriteOfReadStartedBeforeIt()
    {
        // Arrange: a entrada venceu em t0, e a leitura que começa em t0, antes da troca do carimbo, fica presa
        (ControlledStampSource source, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid oldStamp, Guid newStamp) =
            CreateScenario();
        await prototype.DecideAsync(userId, oldStamp);
        clock.Advance(Validity);

        StampReadGate lateReadGate = source.HoldNextRead();
        long oldReadStartedAt = clock.GetTimestamp();
        Task<StampAgeDecision> lateRead = prototype.DecideAsync(userId, oldStamp);
        await lateReadGate.Started.WaitAsync(WaitTimeout);

        clock.Advance(HalfSecond);
        source.SetStamp(userId, newStamp);

        // Act: a invalidação remove a entrada, e a leitura antiga grava depois dela
        prototype.Invalidate(userId);
        clock.Advance(HalfSecond);
        lateReadGate.Release();
        StampAgeDecision lateWrite = await lateRead;

        AdvanceTo(clock, oldReadStartedAt, Validity - OneTick);
        StampAgeDecision atLastTick = await prototype.DecideAsync(userId, oldStamp);
        clock.Advance(OneTick);
        StampAgeDecision atValidity = await prototype.DecideAsync(userId, oldStamp);

        // Assert
        lateWrite.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        KindsOf(prototype, lateWrite).ShouldBe([.. StoredEntryExpired, .. FreshReadStored, Accepted]);
        atLastTick.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        AgeOf(prototype, atLastTick, StoredAgeMeasured).ShouldBe(Validity - OneTick);
        atValidity.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(prototype, atValidity).ShouldBe([.. StoredEntryExpired, .. FreshReadStored, Rejected]);
    }

    [Fact]
    public async Task DecideAsync_Should_RejectPreviousCredentialStamp_WhenPasswordChangeRenewsItAndEntryIsInvalidated()
    {
        // Arrange: um usuário com credencial real, e o protótipo lendo o carimbo dela no PostgreSQL da coleção
        using HttpClient client = factory.CreateClient();
        (Guid userId, string accessToken) = await CreateActiveUserAsync(client);
        Guid stampBefore = await ReadCredentialStampAsync(userId, CancellationToken.None);
        var prototype = new StampAgeCachePrototype(new ControlledTimeProvider(), ReadCredentialStampAsync, Validity);
        StampAgeDecision beforeChange = await prototype.DecideAsync(userId, stampBefore);

        // Act: a troca de senha renova o carimbo, e o protótipo invalida a entrada do usuário
        using var passwordChange = new HttpRequestMessage(HttpMethod.Put, "users/me/password")
        {
            Content = JsonContent.Create(new { currentPassword = UserPassword, newPassword = NewUserPassword })
        };
        passwordChange.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage passwordChangeResponse = await client.SendAsync(passwordChange);

        prototype.Invalidate(userId);
        StampAgeDecision withPreviousStamp = await prototype.DecideAsync(userId, stampBefore);
        Guid stampAfter = await ReadCredentialStampAsync(userId, CancellationToken.None);
        StampAgeDecision withCurrentStamp = await prototype.DecideAsync(userId, stampAfter);

        // Assert
        beforeChange.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        passwordChangeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        stampAfter.ShouldNotBe(stampBefore);
        withPreviousStamp.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(prototype, withPreviousStamp).ShouldBe([EntryAbsent, .. FreshReadStored, Rejected]);
        withCurrentStamp.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        KindsOf(prototype, withCurrentStamp).ShouldBe([.. StoredEntryValid, Accepted]);
        prototype.SourceReadCount.ShouldBe(2);
    }

    [Fact]
    public async Task DecideAsync_Should_LetOldReadOverwriteNewerValueWithoutExtendingItsWindow_WhenItEndsBeforeValidity()
    {
        // Arrange: a leitura A, antiga, começa em t0 e fica presa; o carimbo troca; a leitura B, nova, termina e grava
        // primeiro
        (ControlledStampSource source, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid oldStamp, Guid newStamp) =
            CreateScenario();
        StampReadGate oldReadGate = source.HoldNextRead();
        long oldReadStartedAt = clock.GetTimestamp();
        Task<StampAgeDecision> oldRead = prototype.DecideAsync(userId, oldStamp);
        await oldReadGate.Started.WaitAsync(WaitTimeout);

        clock.Advance(HalfSecond);
        source.SetStamp(userId, newStamp);
        StampAgeDecision newRead = await prototype.DecideAsync(userId, newStamp);

        // Act: a A termina em t0 + V − 1 s e grava sem condição
        AdvanceTo(clock, oldReadStartedAt, Validity - OneSecond);
        oldReadGate.Release();
        StampAgeDecision oldReadDecision = await oldRead;
        StampAgeDecision newStampAfterOverwrite = await prototype.DecideAsync(userId, newStamp);

        AdvanceTo(clock, oldReadStartedAt, Validity - OneTick);
        StampAgeDecision oldStampAtLastTick = await prototype.DecideAsync(userId, oldStamp);
        clock.Advance(OneTick);
        StampAgeDecision oldStampAtValidity = await prototype.DecideAsync(userId, oldStamp);
        StampAgeDecision newStampAfterwards = await prototype.DecideAsync(userId, newStamp);

        // Assert
        newRead.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        oldReadDecision.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        KindsOf(prototype, oldReadDecision).ShouldBe([EntryAbsent, .. FreshReadStored, Accepted]);
        AgeOf(prototype, oldReadDecision, FreshAgeMeasured).ShouldBe(Validity - OneSecond);

        newStampAfterOverwrite.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        AgeOf(prototype, newStampAfterOverwrite, StoredAgeMeasured).ShouldBe(Validity - OneSecond);

        oldStampAtLastTick.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        AgeOf(prototype, oldStampAtLastTick, StoredAgeMeasured).ShouldBe(Validity - OneTick);
        oldStampAtValidity.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(prototype, oldStampAtValidity).ShouldBe([.. StoredEntryExpired, .. FreshReadStored, Rejected]);
        newStampAfterwards.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        KindsOf(prototype, newStampAfterwards).ShouldBe([.. StoredEntryValid, Accepted]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task DecideAsync_Should_RefuseOldReadResultBeforeStoringIt_WhenItEndsAtOrAfterValidity(int millisecondsAfterValidity)
    {
        // Arrange: a leitura A, antiga, começa em t0 e fica presa; o carimbo troca em t0 + 0,5 s; a leitura B, nova,
        // termina e grava primeiro
        (ControlledStampSource source, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid oldStamp, Guid newStamp) =
            CreateScenario();
        StampReadGate oldReadGate = source.HoldNextRead();
        long oldReadStartedAt = clock.GetTimestamp();
        Task<StampAgeDecision> oldRead = prototype.DecideAsync(userId, oldStamp);
        await oldReadGate.Started.WaitAsync(WaitTimeout);

        clock.Advance(HalfSecond);
        source.SetStamp(userId, newStamp);
        StampAgeDecision newRead = await prototype.DecideAsync(userId, newStamp);
        TimeSpan oldReadDuration = Validity + TimeSpan.FromMilliseconds(millisecondsAfterValidity);

        // Act: a A termina em t0 + V ou depois
        AdvanceTo(clock, oldReadStartedAt, oldReadDuration);
        oldReadGate.Release();
        StampAgeDecision oldReadDecision = await oldRead;
        StampAgeDecision newStampAfterwards = await prototype.DecideAsync(userId, newStamp);
        StampAgeDecision oldStampAfterwards = await prototype.DecideAsync(userId, oldStamp);

        // Assert
        newRead.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        oldReadDecision.Outcome.ShouldBe(StampAgeOutcome.FailedClosed);
        KindsOf(prototype, oldReadDecision).ShouldBe(
            [EntryAbsent, SourceReadStarted, SourceReadCompleted, FreshAgeMeasured, FreshAgeNotNegative, FreshAgeExpired, FailedClosed]);
        AgeOf(prototype, oldReadDecision, FreshAgeMeasured).ShouldBe(oldReadDuration);

        // A idade medida, contada de t0 + 0,5 s, prova que o dicionário guarda o valor da B, e não o resultado recusado.
        // Com a A terminando 1 s depois de V, o valor da B também já venceu e é lido de novo.
        List<StampAgeStepKind> expectedAfterwards = millisecondsAfterValidity == 0
            ? [.. StoredEntryValid, Accepted]
            : [.. StoredEntryExpired, .. FreshReadStored, Accepted];

        newStampAfterwards.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        AgeOf(prototype, newStampAfterwards, StoredAgeMeasured).ShouldBe(oldReadDuration - HalfSecond);
        KindsOf(prototype, newStampAfterwards).ShouldBe(expectedAfterwards);
        oldStampAfterwards.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        KindsOf(prototype, oldStampAfterwards).ShouldBe([.. StoredEntryValid, Rejected]);
        prototype.SourceReadCount.ShouldBe(millisecondsAfterValidity == 0 ? 2 : 3);
    }

    [Fact]
    public async Task DecideAsync_Should_FailClosedWithoutStoringUsingOrRetrying_WhenFreshReadAgeReachesValidity()
    {
        // Arrange: uma entrada vencida, e a leitura nova presa até a idade dela chegar a V
        (ControlledStampSource source, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid oldStamp, Guid newStamp) =
            CreateScenario();
        await prototype.DecideAsync(userId, oldStamp);
        clock.Advance(Validity + OneSecond);
        source.SetStamp(userId, newStamp);

        StampReadGate freshReadGate = source.HoldNextRead();
        int readsBefore = prototype.SourceReadCount;
        Task<StampAgeDecision> pendingDecision = prototype.DecideAsync(userId, newStamp);
        await freshReadGate.Started.WaitAsync(WaitTimeout);

        // Act: a leitura termina quando a idade dela chega a V
        clock.Advance(Validity);
        freshReadGate.Release();
        StampAgeDecision decision = await pendingDecision;
        int readsForDecision = prototype.SourceReadCount - readsBefore;
        StampAgeDecision nextDecision = await prototype.DecideAsync(userId, newStamp);

        // Assert
        decision.Outcome.ShouldBe(StampAgeOutcome.FailedClosed);
        readsForDecision.ShouldBe(1);
        KindsOf(prototype, decision).ShouldBe(
            [.. StoredEntryExpired, SourceReadStarted, SourceReadCompleted, FreshAgeMeasured, FreshAgeNotNegative, FreshAgeExpired, FailedClosed]);
        AgeOf(prototype, decision, FreshAgeMeasured).ShouldBe(Validity);

        // O dicionário continua com a entrada vencida, e não com o resultado recusado.
        AgeOf(prototype, nextDecision, StoredAgeMeasured).ShouldBe(Validity + OneSecond + Validity);
    }

    [Fact]
    public async Task DecideAsync_Should_FailClosedWithoutStoringOrRetrying_WhenInjectedClockRegressionMakesFreshAgeNegative()
    {
        // Arrange: injeção de falha. Um relógio de teste montado sobre o relógio controlado; a leitura nova guarda o
        // instante em que começa e fica presa
        var source = new ControlledStampSource();
        var userId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        source.SetStamp(userId, stamp);

        var clock = new RegressingTimeProvider(new ControlledTimeProvider(1_000_000_000));
        var prototype = new StampAgeCachePrototype(clock, source.ReadAsync, Validity);
        StampReadGate heldReadGate = source.HoldNextRead();

        long readStartedAt = clock.GetTimestamp();
        Task<StampAgeDecision> pendingDecision = prototype.DecideAsync(userId, stamp);
        await heldReadGate.Started.WaitAsync(WaitTimeout);

        // Act: o instante regride 1 s durante a leitura presa, e a leitura é liberada
        clock.Regress(OneSecond);
        long regressedTo = clock.GetTimestamp();
        heldReadGate.Release();
        StampAgeDecision decision = await pendingDecision;
        int readsForDecision = prototype.SourceReadCount;
        StampAgeDecision nextDecision = await prototype.DecideAsync(userId, stamp);

        // Assert
        (regressedTo - readStartedAt).ShouldBe(-OneSecond.Ticks);
        decision.Outcome.ShouldBe(StampAgeOutcome.FailedClosed);
        KindsOf(prototype, decision).ShouldBe(
            [EntryAbsent, SourceReadStarted, SourceReadCompleted, FreshAgeMeasured, FreshAgeNegative, FailedClosed]);
        AgeOf(prototype, decision, FreshAgeMeasured).ShouldBe(-OneSecond);
        readsForDecision.ShouldBe(1);

        // O resultado recusado não foi gravado: a decisão seguinte não encontra entrada e lê a fonte de novo.
        KindsOf(prototype, nextDecision).ShouldBe([EntryAbsent, .. FreshReadStored, Accepted]);
        prototype.SourceReadCount.ShouldBe(2);
    }

    [Fact]
    public async Task Prototype_Should_KeepStampsOutOfDecisionsAndDiagnostics()
    {
        // Arrange
        (_, ControlledTimeProvider clock, StampAgeCachePrototype prototype, Guid userId, Guid stamp, Guid otherStamp) = CreateScenario();

        // Act: a decisão, a invalidação, o identificador, o método de teste e os diagnósticos
        StampAgeDecision withRead = await prototype.DecideAsync(userId, stamp);
        StampAgeDecision fromEntry = await prototype.DecideAsync(userId, otherStamp);
        prototype.Invalidate(userId);
        prototype.PlantForTest(userId, new StampAgeEntry(otherStamp, clock.GetTimestamp(), Guid.NewGuid()));
        StampAgeDecision withPlantedEntry = await prototype.DecideAsync(userId, otherStamp);
        Guid instanceId = prototype.InstanceId;
        IReadOnlyList<StampAgeStep> steps = prototype.Steps;
        int sourceReads = prototype.SourceReadCount;

        string exposed = string.Join(
            Environment.NewLine,
            [withRead.ToString(), fromEntry.ToString(), withPlantedEntry.ToString(), .. steps.Select(step => step.ToString())]);

        // Assert
        withRead.Outcome.ShouldBe(StampAgeOutcome.Accepted);
        fromEntry.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        withPlantedEntry.Outcome.ShouldBe(StampAgeOutcome.Rejected);
        sourceReads.ShouldBe(2);
        steps.Select(step => step.DecisionId).Distinct().ShouldBe([withRead.Id, fromEntry.Id, withPlantedEntry.Id]);
        instanceId.ShouldNotBe(Guid.Empty);
        instanceId.ShouldNotBeOneOf(stamp, otherStamp);
        exposed.ShouldNotContain(stamp.ToString(), Case.Insensitive);
        exposed.ShouldNotContain(otherStamp.ToString(), Case.Insensitive);
    }

    private static List<StampAgeStepKind> ExpectedSteps(EntrySituation situation) => situation switch
    {
        EntrySituation.Absent => [EntryAbsent, .. FreshReadStored, Rejected],
        EntrySituation.FromAnotherInstance => [EntryPresent, InstanceDiffers, .. FreshReadStored, Rejected],
        EntrySituation.NegativeAge =>
            [EntryPresent, InstanceMatches, StoredAgeMeasured, StoredAgeNegative, .. FreshReadStored, Rejected],
        EntrySituation.AgeEqualToValidity => [.. StoredEntryExpired, .. FreshReadStored, Rejected],
        _ => [.. StoredEntryValid, Accepted]
    };

    // Em cada instância, uma leitura com o carimbo antigo começa em t0 e fica presa; o carimbo troca em t0 + 0,5 s, com uma
    // decisão logo depois; a leitura presa termina e grava em t0 + 1 s; seguem decisões com o carimbo antigo nos instantes
    // de OffsetsAfterOldWrite. Devolve, por instância, cada decisão com o tempo desde t0 no relógio dela.
    private static async Task<List<TimedDecision>[]> RunStampChangeAsync(
        ControlledStampSource source,
        Guid userId,
        Guid oldStamp,
        Guid newStamp,
        params (StampAgeCachePrototype Prototype, ControlledTimeProvider Clock)[] instances)
    {
        var heldReads = new List<(StampReadGate Gate, Task<StampAgeDecision> Decision)>();

        foreach ((StampAgeCachePrototype prototype, ControlledTimeProvider _) in instances)
        {
            StampReadGate gate = source.HoldNextRead();
            Task<StampAgeDecision> decision = prototype.DecideAsync(userId, oldStamp);
            await gate.Started.WaitAsync(WaitTimeout);
            heldReads.Add((gate, decision));
        }

        long[] oldReadStarts = [.. instances.Select(instance => instance.Clock.GetTimestamp())];
        List<TimedDecision>[] decisions = [.. instances.Select(_ => new List<TimedDecision>())];

        AdvanceAll(HalfSecond);
        source.SetStamp(userId, newStamp);
        await DecideWithOldStampAsync();

        AdvanceAll(HalfSecond);

        for (int index = 0; index < instances.Length; index++)
        {
            heldReads[index].Gate.Release();
            Record(index, await heldReads[index].Decision);
        }

        foreach (TimeSpan offset in OffsetsAfterOldWrite)
        {
            for (int index = 0; index < instances.Length; index++)
            {
                AdvanceTo(instances[index].Clock, oldReadStarts[index], offset);
            }

            await DecideWithOldStampAsync();
        }

        return decisions;

        void AdvanceAll(TimeSpan duration)
        {
            foreach ((StampAgeCachePrototype _, ControlledTimeProvider clock) in instances)
            {
                clock.Advance(duration);
            }
        }

        void Record(int index, StampAgeDecision decision) =>
            decisions[index].Add(new TimedDecision(instances[index].Clock.GetElapsedTime(oldReadStarts[index]), decision.Outcome));

        async Task DecideWithOldStampAsync()
        {
            for (int index = 0; index < instances.Length; index++)
            {
                Record(index, await instances[index].Prototype.DecideAsync(userId, oldStamp));
            }
        }
    }

    private static void ShouldAcceptOldStampOnlyBeforeValidity(List<TimedDecision> decisions)
    {
        TimeSpan lastAccepted = decisions.Where(decision => decision.Outcome == StampAgeOutcome.Accepted).Max(decision => decision.SinceOldReadStart);

        lastAccepted.ShouldBe(Validity - OneTick);
        decisions.Where(decision => decision.SinceOldReadStart >= Validity).ShouldAllBe(decision => decision.Outcome == StampAgeOutcome.Rejected);
        decisions.ShouldContain(decision => decision.SinceOldReadStart == Validity);
    }

    private static (StampAgeCachePrototype Prototype, ControlledTimeProvider Clock) CreateInstance(
        ControlledStampSource source,
        long startTimestamp)
    {
        var clock = new ControlledTimeProvider(startTimestamp);

        return (new StampAgeCachePrototype(clock, source.ReadAsync, Validity), clock);
    }

    // Uma instância com o próprio relógio e uma fonte com o carimbo antigo do usuário.
    private static Scenario CreateScenario()
    {
        var source = new ControlledStampSource();
        var clock = new ControlledTimeProvider(1_000_000_000);
        var scenario = new Scenario(
            source,
            clock,
            new StampAgeCachePrototype(clock, source.ReadAsync, Validity),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        source.SetStamp(scenario.UserId, scenario.OldStamp);

        return scenario;
    }

    private static void AdvanceTo(ControlledTimeProvider clock, long origin, TimeSpan offset) =>
        clock.Advance(offset - clock.GetElapsedTime(origin));

    private static List<StampAgeStepKind> KindsOf(StampAgeCachePrototype prototype, StampAgeDecision decision) =>
        [.. prototype.Steps.Where(step => step.DecisionId == decision.Id).Select(step => step.Kind)];

    private static TimeSpan AgeOf(StampAgeCachePrototype prototype, StampAgeDecision decision, StampAgeStepKind measuredStep) =>
        prototype.Steps.Single(step => step.DecisionId == decision.Id && step.Kind == measuredStep).Age
        ?? throw new InvalidOperationException("O passo não registrou a idade.");

    // Declarações públicas de primeiro nível da classe do protótipo (recuo de 4 espaços), sem o corpo: o tipo e o nome.
    private static List<string> PublicMemberDeclarations(string source)
    {
        const string prefix = "    public ";
        var declarations = new List<string>();

        foreach (string line in source.Split('\n'))
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string declaration = line[prefix.Length..];
            int end = declaration.IndexOfAny(['=', '(', '{']);
            declarations.Add((end < 0 ? declaration : declaration[..end]).Trim());
        }

        return declarations;
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;

        for (int index = text.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    // Arquivo do protótipo, a partir da raiz do repositório (a pasta com a solução).
    private static string PrototypeSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TickestPristine.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();

        return Path.Combine(
            directory.FullName,
            "tests",
            "TickestPristine.IntegrationTests",
            "Caching",
            "StampAge",
            "StampAgeCachePrototype.cs");
    }

    // Chamadas de cada comando de dados no Redis desde que ele subiu, sem os comandos de conexão e de administração.
    private static async Task<Dictionary<string, long>> DataCommandCallsAsync(IServer server)
    {
        IGrouping<string, KeyValuePair<string, string>>[] sections = await server.InfoAsync("commandstats");
        var calls = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> statistic in sections.SelectMany(section => section))
        {
            const string prefix = "cmdstat_";

            if (!statistic.Key.StartsWith(prefix, StringComparison.Ordinal) ||
                RedisConnectionCommands.Contains(statistic.Key[prefix.Length..].Split('|')[0]))
            {
                continue;
            }

            string callsText = statistic.Value.Split(',')[0]["calls=".Length..];
            calls[statistic.Key] = long.Parse(callsText, CultureInfo.InvariantCulture);
        }

        return calls;
    }

    // O HybridCache e o cliente do Redis podem mandar comandos em segundo plano logo depois de uma operação: espera os
    // contadores ficarem iguais em duas leituras seguidas, por até 2 s, antes de medir.
    private static async Task<Dictionary<string, long>> StableDataCommandCallsAsync(IServer server)
    {
        Dictionary<string, long> previous = await DataCommandCallsAsync(server);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Dictionary<string, long> current = await DataCommandCallsAsync(server);

            if (IncreasedCommands(previous, current).Count == 0)
            {
                return current;
            }

            previous = current;
        }

        return previous;
    }

    // A gravação no Redis pode terminar depois da operação: espera a chave aparecer, por até 1 s.
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

    private static List<string> IncreasedCommands(Dictionary<string, long> before, Dictionary<string, long> after) =>
        [.. after.Where(command => command.Value > before.GetValueOrDefault(command.Key)).Select(command => command.Key)];

    private static async Task<List<string>> KeysOfAsync(IServer server, Guid userId)
    {
        var keys = new List<string>();

        await foreach (RedisKey key in server.KeysAsync(pattern: $"*{userId}*"))
        {
            keys.Add(key.ToString());
        }

        return keys;
    }

    // Cadastra o usuário pelo administrador, ativa a conta pelo token do convite e faz o login.
    private async Task<(Guid UserId, string AccessToken)> CreateActiveUserAsync(HttpClient client)
    {
        string email = $"prototipo-{Guid.NewGuid():N}@example.com";
        string adminToken = await LoginAsync(client, IntegrationTestWebAppFactory.AdminEmail, IntegrationTestWebAppFactory.AdminPassword);

        using var registration = new HttpRequestMessage(HttpMethod.Post, "users/register")
        {
            Content = JsonContent.Create(new { email, firstName = "Test", lastName = "User" })
        };
        registration.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using HttpResponseMessage registrationResponse = await client.SendAsync(registration);
        registrationResponse.EnsureSuccessStatusCode();
        Guid userId = await registrationResponse.Content.ReadFromJsonAsync<Guid>();

        string invitationToken = factory.Services.GetRequiredService<CapturingEmailSender>().LatestTokenFor(email);
        using HttpResponseMessage activation =
            await client.PostAsJsonAsync("users/activation", new { token = invitationToken, newPassword = UserPassword });
        activation.EnsureSuccessStatusCode();

        return (userId, await LoginAsync(client, email, UserPassword));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("users/login", new { email, password });
        response.EnsureSuccessStatusCode();
        AccessTokens? tokens = await response.Content.ReadFromJsonAsync<AccessTokens>();

        return tokens!.AccessToken;
    }

    private async Task<Guid> ReadCredentialStampAsync(Guid userId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.UserCredentials
            .Where(credential => credential.UserId == userId)
            .Select(credential => credential.SecurityStamp)
            .SingleAsync(cancellationToken);
    }

    private sealed record Scenario(
        ControlledStampSource Source,
        ControlledTimeProvider Clock,
        StampAgeCachePrototype Prototype,
        Guid UserId,
        Guid OldStamp,
        Guid NewStamp);

    private sealed record TimedDecision(TimeSpan SinceOldReadStart, StampAgeOutcome Outcome);

    private sealed record AccessTokens(string AccessToken, string RefreshToken);

    // Só para a injeção de falha: relógio de teste sobre o relógio controlado que pode fazer o instante monotônico
    // regredir. O relógio controlado continua nunca voltando, e a regressão não representa um relógio monotônico válido.
    private sealed class RegressingTimeProvider(ControlledTimeProvider controlled) : TimeProvider
    {
        private long _regressionTicks;

        public override long TimestampFrequency => controlled.TimestampFrequency;

        public override long GetTimestamp() => controlled.GetTimestamp() - Interlocked.Read(ref _regressionTicks);

        public override DateTimeOffset GetUtcNow() => controlled.GetUtcNow();

        public void Regress(TimeSpan duration) => Interlocked.Add(ref _regressionTicks, duration.Ticks);
    }
}
