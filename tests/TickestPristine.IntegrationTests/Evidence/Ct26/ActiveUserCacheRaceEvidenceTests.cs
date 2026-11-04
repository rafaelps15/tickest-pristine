using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Web.Api;
using Xunit.Abstractions;

namespace TickestPristine.IntegrationTests.Evidence.Ct26;

[Collection(nameof(Ct26EvidenceCollection))]
public sealed class ActiveUserCacheRaceEvidenceTests(Ct26EvidenceFixture fixture, ITestOutputHelper output)
{
    // Só a política padrão, que confere a conta ativa, protege esta rota.
    private const string Route = "departments";
    private const string UserPassword = "Password123!";

    // Validades do cache só nos hosts destas evidências: 6 s no total e 3 s na memória.
    private static readonly TimeSpan TotalValidity = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan MemoryValidity = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HeldReadDuration = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan PathProofHold = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan ObservationMargin = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    [EvidenceFact]
    public async Task GetDepartments_Should_AcceptDeactivatedUserUntilLateWritePlusMemoryValidity_WhenActiveCheckEndsAfterDeactivation()
    {
        // Arrange: uma instância sem Redis e um usuário ativo, com token; a consulta de conta ativa fica presa
        await using var instance = new ApiInstance(fixture.Api, redisConnectionString: null);
        using HttpClient admin = await instance.CreateAdminClientAsync();
        (Guid userId, string accessToken) = await CreateActiveUserAsync(instance, admin);
        using HttpClient user = instance.CreateClient(accessToken);

        instance.Gate.HoldNextMatch();
        long readStartedAt = Stopwatch.GetTimestamp();
        Task<HttpResponseMessage> heldRequest = user.GetAsync(Route);
        await instance.Gate.WaitUntilHeldAsync(WaitTimeout);

        // Act: o administrador desativa o usuário (commit e invalidação), e só depois a consulta é liberada
        using HttpResponseMessage deactivation = await admin.PutAsync($"users/{userId}/deactivate", null);
        await DelayUntilAsync(readStartedAt, HeldReadDuration);
        bool endedBeforeRelease = heldRequest.IsCompleted;
        instance.Gate.Release();
        using HttpResponseMessage heldResponse = await heldRequest;
        long writtenAt = Stopwatch.GetTimestamp();
        bool memoryHasEntry = await WaitUntilAsync(() => instance.MemoryHas(ActiveUserKey(userId)));

        List<ApiObservation> observations =
            await PollAsync(user, writtenAt, MemoryValidity + ObservationMargin, instance.Gate);

        // Assert
        Record($"requisição presa terminou antes da liberação: {endedBeforeRelease}; status: {(int)heldResponse.StatusCode}; entrada na memória: {memoryHasEntry}");
        Record($"último 200 em gravação + {Seconds(LastElapsed(observations, HttpStatusCode.OK))} s; primeiro 403 em gravação + {Seconds(FirstElapsed(observations, HttpStatusCode.Forbidden))} s");

        deactivation.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        endedBeforeRelease.ShouldBeFalse();
        heldResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        memoryHasEntry.ShouldBeTrue();
        ShouldHoldAround(
            observations,
            MemoryValidity,
            before: o => o.Status == HttpStatusCode.OK,
            after: o => o.Status == HttpStatusCode.Forbidden);
    }

    [EvidenceFact]
    public async Task GetDepartments_Should_AcceptDeactivatedUserOnOtherInstanceUntilItsOwnEntryExpires_WithoutRedis()
    {
        // Arrange: duas instâncias sem Redis; a B já gravou a entrada de conta ativa pela própria consulta
        await using var instanceA = new ApiInstance(fixture.Api, redisConnectionString: null);
        await using var instanceB = new ApiInstance(fixture.Api, redisConnectionString: null);
        using HttpClient admin = await instanceA.CreateAdminClientAsync();
        (Guid userId, string accessToken) = await CreateActiveUserAsync(instanceA, admin);
        using HttpClient userOnA = instanceA.CreateClient(accessToken);
        using HttpClient userOnB = instanceB.CreateClient(accessToken);

        // A consulta de conta ativa da B fica presa por um instante, para provar que a requisição passa por ela.
        instanceB.Gate.HoldNextMatch();
        Task<HttpResponseMessage> firstOnBRequest = userOnB.GetAsync(Route);
        await instanceB.Gate.WaitUntilHeldAsync(WaitTimeout);
        await Task.Delay(PathProofHold);
        bool firstOnBEndedBeforeRelease = firstOnBRequest.IsCompleted;
        instanceB.Gate.Release();
        using HttpResponseMessage firstOnB = await firstOnBRequest;
        long writtenOnB = Stopwatch.GetTimestamp();
        bool memoryOfBHasEntry = await WaitUntilAsync(() => instanceB.MemoryHas(ActiveUserKey(userId)));

        // Act: a desativação acontece na A
        using HttpResponseMessage deactivation = await admin.PutAsync($"users/{userId}/deactivate", null);
        using HttpResponseMessage nextOnA = await userOnA.GetAsync(Route);

        List<ApiObservation> observationsOnB =
            await PollAsync(userOnB, writtenOnB, MemoryValidity + ObservationMargin, instanceB.Gate);

        // Assert
        Record($"primeira requisição na B terminou antes da liberação: {firstOnBEndedBeforeRelease}; entrada na memória da B: {memoryOfBHasEntry}");
        Record($"A depois da desativação: {(int)nextOnA.StatusCode}");
        Record($"B: último 200 em gravação na B + {Seconds(LastElapsed(observationsOnB, HttpStatusCode.OK))} s; primeiro 403 em gravação na B + {Seconds(FirstElapsed(observationsOnB, HttpStatusCode.Forbidden))} s");

        firstOnBEndedBeforeRelease.ShouldBeFalse();
        firstOnB.StatusCode.ShouldBe(HttpStatusCode.OK);
        memoryOfBHasEntry.ShouldBeTrue();
        deactivation.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        nextOnA.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ShouldHoldAround(
            observationsOnB,
            MemoryValidity,
            before: o => o.Status == HttpStatusCode.OK,
            after: o => o.Status == HttpStatusCode.Forbidden);
    }

    [EvidenceFact]
    public async Task GetDepartments_Should_AcceptDeactivatedUserOnOtherInstanceThroughRedisUntilReadStartPlusTotalValidity()
    {
        // Arrange: duas instâncias com o mesmo Redis; a consulta de conta ativa fica presa na A
        await using var instanceA = new ApiInstance(fixture.Api, fixture.Redis.ConnectionString);
        await using var instanceB = new ApiInstance(fixture.Api, fixture.Redis.ConnectionString);
        await using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(fixture.Redis.ConnectionString);
        using HttpClient admin = await instanceA.CreateAdminClientAsync();
        (Guid userId, string accessToken) = await CreateActiveUserAsync(instanceA, admin);
        using HttpClient userOnA = instanceA.CreateClient(accessToken);
        using HttpClient userOnB = instanceB.CreateClient(accessToken);
        int checksOnABefore = instanceA.Gate.MatchedCount;
        int checksOnBBefore = instanceB.Gate.MatchedCount;

        instanceA.Gate.HoldNextMatch();
        long readStartedAt = Stopwatch.GetTimestamp();
        Task<HttpResponseMessage> heldRequest = userOnA.GetAsync(Route);
        await instanceA.Gate.WaitUntilHeldAsync(WaitTimeout);

        // Act: a desativação acontece na A, e só depois a consulta é liberada
        using HttpResponseMessage deactivation = await admin.PutAsync($"users/{userId}/deactivate", null);
        await DelayUntilAsync(readStartedAt, HeldReadDuration);
        bool endedBeforeRelease = heldRequest.IsCompleted;
        instanceA.Gate.Release();
        using HttpResponseMessage heldResponse = await heldRequest;
        long writtenAt = Stopwatch.GetTimestamp();
        bool memoryOfAHasEntry = await WaitUntilAsync(() => instanceA.MemoryHas(ActiveUserKey(userId)));
        IDatabase database = redis.GetDatabase();
        bool storedInRedis = await WaitUntilAsync(() => database.KeyExists(ActiveUserKey(userId)));

        var observationsOnA = new List<ApiObservation>();
        var observationsOnB = new List<ApiObservation>();

        while (Stopwatch.GetElapsedTime(readStartedAt) <= TotalValidity + ObservationMargin)
        {
            if (Stopwatch.GetElapsedTime(writtenAt) <= MemoryValidity - Tolerance)
            {
                observationsOnA.Add(await ObserveAsync(userOnA, writtenAt, instanceA.Gate, checksOnABefore));
            }

            observationsOnB.Add(await ObserveAsync(userOnB, readStartedAt, instanceB.Gate, checksOnBBefore));

            await Task.Delay(PollInterval);
        }

        // Assert
        Record($"leitura presa por {Seconds(Stopwatch.GetElapsedTime(readStartedAt, writtenAt))} s; terminou antes da liberação: {endedBeforeRelease}; status na A: {(int)heldResponse.StatusCode}; entrada na memória da A: {memoryOfAHasEntry}; chave no Redis: {storedInRedis}");
        Record($"B: último 200 em início da leitura na A + {Seconds(LastElapsed(observationsOnB, HttpStatusCode.OK))} s; primeiro 403 em início + {Seconds(FirstElapsed(observationsOnB, HttpStatusCode.Forbidden))} s");
        Record($"consultas de conta ativa da B enquanto respondia 200: {observationsOnB.Where(o => o.Status == HttpStatusCode.OK).Select(o => o.ActiveChecks).DefaultIfEmpty().Max()}");

        deactivation.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        endedBeforeRelease.ShouldBeFalse();
        heldResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        memoryOfAHasEntry.ShouldBeTrue();
        storedInRedis.ShouldBeTrue();
        observationsOnA.ShouldNotBeEmpty();
        observationsOnA.ShouldAllBe(o => o.Status == HttpStatusCode.OK);
        ShouldHoldAround(
            observationsOnB,
            TotalValidity,
            before: o => o.Status == HttpStatusCode.OK && o.ActiveChecks == 0,
            after: o => o.Status == HttpStatusCode.Forbidden);
        observationsOnB.Where(o => o.Status == HttpStatusCode.OK).ShouldAllBe(o => o.ActiveChecks == 0);
    }

    [EvidenceFact]
    public async Task GetDepartments_Should_JoinHeldActiveCheckAndAccept_WhenRequestArrivesAfterDeactivation()
    {
        // Arrange: uma instância com a consulta de conta ativa presa; o usuário é desativado durante ela
        await using var instance = new ApiInstance(fixture.Api, redisConnectionString: null);
        using HttpClient admin = await instance.CreateAdminClientAsync();
        (Guid userId, string accessToken) = await CreateActiveUserAsync(instance, admin);
        using HttpClient user = instance.CreateClient(accessToken);
        int checksBefore = instance.Gate.MatchedCount;

        instance.Gate.HoldNextMatch();
        Task<HttpResponseMessage> heldRequest = user.GetAsync(Route);
        await instance.Gate.WaitUntilHeldAsync(WaitTimeout);
        using HttpResponseMessage deactivation = await admin.PutAsync($"users/{userId}/deactivate", null);

        // Act: outra requisição do mesmo usuário chega durante a consulta presa
        Task<HttpResponseMessage> laterRequest = user.GetAsync(Route);
        await Task.Delay(TimeSpan.FromSeconds(1));
        bool heldEndedBeforeRelease = heldRequest.IsCompleted;
        bool laterEndedBeforeRelease = laterRequest.IsCompleted;
        instance.Gate.Release();
        using HttpResponseMessage heldResponse = await heldRequest;
        using HttpResponseMessage laterResponse = await laterRequest;
        int activeChecks = instance.Gate.MatchedCount - checksBefore;
        bool memoryHasEntry = await WaitUntilAsync(() => instance.MemoryHas(ActiveUserKey(userId)));

        // Assert
        Record($"a primeira requisição terminou antes da liberação: {heldEndedBeforeRelease}; entrada na memória: {memoryHasEntry}");
        Record($"a segunda requisição terminou antes da liberação: {laterEndedBeforeRelease}; status: {(int)laterResponse.StatusCode}; consultas de conta ativa: {activeChecks}");

        deactivation.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        heldEndedBeforeRelease.ShouldBeFalse();
        laterEndedBeforeRelease.ShouldBeFalse();
        heldResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        memoryHasEntry.ShouldBeTrue();
        laterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        activeChecks.ShouldBe(1);
    }

    // Mesma chave que o provedor de permissões usa para a conta ativa.
    private static string ActiveUserKey(Guid userId) => $"user-active-{userId}";

    // Cadastra o usuário pelo administrador, ativa a conta pelo token do convite e faz o login.
    private static async Task<(Guid UserId, string AccessToken)> CreateActiveUserAsync(ApiInstance instance, HttpClient admin)
    {
        string email = $"evidencia-{Guid.NewGuid():N}@example.com";

        using HttpResponseMessage registration =
            await admin.PostAsJsonAsync("users/register", new { email, firstName = "Test", lastName = "User" });
        registration.EnsureSuccessStatusCode();
        Guid userId = await registration.Content.ReadFromJsonAsync<Guid>();

        using HttpClient anonymous = instance.CreateClient();
        string invitationToken = instance.EmailSender.LatestTokenFor(email);
        using HttpResponseMessage activation =
            await anonymous.PostAsJsonAsync("users/activation", new { token = invitationToken, newPassword = UserPassword });
        activation.EnsureSuccessStatusCode();

        return (userId, await LoginAsync(anonymous, email, UserPassword));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("users/login", new { email, password });
        response.EnsureSuccessStatusCode();
        AccessTokens? tokens = await response.Content.ReadFromJsonAsync<AccessTokens>();

        return tokens!.AccessToken;
    }

    private static async Task<List<ApiObservation>> PollAsync(
        HttpClient client,
        long reference,
        TimeSpan duration,
        EfCommandGate gate)
    {
        int checksBefore = gate.MatchedCount;
        var observations = new List<ApiObservation>();

        while (Stopwatch.GetElapsedTime(reference) <= duration)
        {
            observations.Add(await ObserveAsync(client, reference, gate, checksBefore));

            await Task.Delay(PollInterval);
        }

        return observations;
    }

    // Uma requisição à rota, com o instante em que saiu e as consultas de conta ativa da instância desde a referência.
    private static async Task<ApiObservation> ObserveAsync(HttpClient client, long reference, EfCommandGate gate, int checksBefore)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(reference);
        using HttpResponseMessage response = await client.GetAsync(Route);

        return new ApiObservation(elapsed, response.StatusCode, gate.MatchedCount - checksBefore);
    }

    // Confere o comportamento antes e depois do limite, fora da faixa de tolerância em volta dele.
    private static void ShouldHoldAround(
        List<ApiObservation> observations,
        TimeSpan boundary,
        Expression<Func<ApiObservation, bool>> before,
        Expression<Func<ApiObservation, bool>> after)
    {
        List<ApiObservation> early = [.. observations.Where(o => o.Elapsed <= boundary - Tolerance)];
        List<ApiObservation> late = [.. observations.Where(o => o.Elapsed >= boundary + Tolerance)];

        early.ShouldNotBeEmpty();
        late.ShouldNotBeEmpty();
        early.ShouldAllBe(before);
        late.ShouldAllBe(after);
    }

    private static async Task DelayUntilAsync(long reference, TimeSpan offset)
    {
        TimeSpan remaining = offset - Stopwatch.GetElapsedTime(reference);

        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }

    // A gravação no cache pode terminar depois da resposta: espera a condição valer, por até 1 s.
    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        long startedAt = Stopwatch.GetTimestamp();

        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(startedAt) > TimeSpan.FromSeconds(1))
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        return true;
    }

    private static TimeSpan? LastElapsed(List<ApiObservation> observations, HttpStatusCode status) =>
        observations.LastOrDefault(o => o.Status == status)?.Elapsed;

    private static TimeSpan? FirstElapsed(List<ApiObservation> observations, HttpStatusCode status) =>
        observations.FirstOrDefault(o => o.Status == status)?.Elapsed;

    private static string Seconds(TimeSpan? value) =>
        value is null ? "nunca" : value.Value.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);

    private void Record(FormattableString text) => output.WriteLine(text.ToString(CultureInfo.InvariantCulture));

    private sealed record ApiObservation(TimeSpan Elapsed, HttpStatusCode Status, int ActiveChecks);

    private sealed record AccessTokens(string AccessToken, string RefreshToken);

    // Uma instância da API: host derivado, com a própria memória, as validades curtas do cache e o portão do EF.
    private sealed class ApiInstance : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _host;

        public ApiInstance(IntegrationTestWebAppFactory api, string? redisConnectionString)
        {
            Gate = new EfCommandGate(IsActiveUserQuery);

            _host = api.WithWebHostBuilder(builder =>
            {
                if (redisConnectionString is not null)
                {
                    builder.UseSetting("ConnectionStrings:Cache", redisConnectionString);
                }

                builder.ConfigureTestServices(services =>
                {
                    services.Configure<HybridCacheOptions>(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
                    {
                        Expiration = TotalValidity,
                        LocalCacheExpiration = MemoryValidity
                    });

                    services.ConfigureDbContext<ApplicationDbContext>(options => options.AddInterceptors(Gate));
                });
            });
        }

        public EfCommandGate Gate { get; }

        public CapturingEmailSender EmailSender => _host.Services.GetRequiredService<CapturingEmailSender>();

        public HttpClient CreateClient(string? accessToken = null)
        {
            HttpClient client = _host.CreateClient();

            if (accessToken is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return client;
        }

        public async Task<HttpClient> CreateAdminClientAsync()
        {
            using HttpClient anonymous = CreateClient();
            string accessToken = await LoginAsync(
                anonymous,
                IntegrationTestWebAppFactory.AdminEmail,
                IntegrationTestWebAppFactory.AdminPassword);

            return CreateClient(accessToken);
        }

        public bool MemoryHas(string key) => _host.Services.GetRequiredService<IMemoryCache>().TryGetValue(key, out _);

        public ValueTask DisposeAsync() => _host.DisposeAsync();

        // A consulta do IsActiveAsync do provedor de permissões: um Any por id e conta ativa na tabela de usuários. A ativação
        // de conta faz a mesma consulta, por isso as contagens partem de depois do cadastro. A do último administrador
        // também passa pela tabela de usuários, mas parte das roles.
        private static bool IsActiveUserQuery(string sql) =>
            sql.StartsWith("SELECT EXISTS", StringComparison.Ordinal) &&
            sql.Contains("users AS u", StringComparison.Ordinal) &&
            sql.Contains("u.is_active", StringComparison.Ordinal) &&
            !sql.Contains("user_roles", StringComparison.Ordinal);
    }
}
