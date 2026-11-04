using TickestPristine.IntegrationTests.Caching;

namespace TickestPristine.IntegrationTests.Evidence.Ct26;

[CollectionDefinition(nameof(Ct26EvidenceCollection), DisableParallelization = true)]
public sealed class Ct26EvidenceCollection : ICollectionFixture<Ct26EvidenceFixture>;

/// <summary>
/// Contêineres das evidências: o PostgreSQL da API e um Redis, que só sobem com a variável de
/// <see cref="EvidenceFactAttribute"/> ligada. Sem ela, os testes da coleção aparecem como ignorados e nenhum contêiner é
/// criado.
/// </summary>
public sealed class Ct26EvidenceFixture : IAsyncLifetime, IAsyncDisposable
{
    private IntegrationTestWebAppFactory? _api;
    private RedisContainerFixture? _redis;

    public IntegrationTestWebAppFactory Api => _api ?? throw NotStarted();

    public RedisContainerFixture Redis => _redis ?? throw NotStarted();

    public async Task InitializeAsync()
    {
        if (!EvidenceFactAttribute.IsEnabled)
        {
            return;
        }

        _redis = new RedisContainerFixture();
        _api = new IntegrationTestWebAppFactory();

        await _redis.InitializeAsync();
        await _api.InitializeAsync();
    }

    /// <summary>
    /// Descarta os contêineres que subiram; chamado mais de uma vez, não faz nada nas chamadas seguintes.
    /// </summary>
    public async Task DisposeAsync()
    {
        IntegrationTestWebAppFactory? api = Interlocked.Exchange(ref _api, null);
        RedisContainerFixture? redis = Interlocked.Exchange(ref _redis, null);

        if (api is not null)
        {
            await api.DisposeAsync();
        }

        if (redis is not null)
        {
            await redis.DisposeAsync();
        }
    }

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    private static InvalidOperationException NotStarted() =>
        new($"Os contêineres das evidências só sobem com {EvidenceFactAttribute.VariableName}=1.");
}
