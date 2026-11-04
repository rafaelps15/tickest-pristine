using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace TickestPristine.IntegrationTests.Caching;

/// <summary>
/// Redis de teste num contêiner do construtor genérico do Testcontainers, sem pacote próprio para o Redis. Cada coleção
/// que o usa como fixture recebe um contêiner exclusivo.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime, IAsyncDisposable
{
    private const int RedisPort = 6379;

    private readonly IContainer _container = new ContainerBuilder("redis:7.4")
        .WithPortBinding(RedisPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "ping"))
        .Build();

    public string ConnectionString => $"{_container.Hostname}:{_container.GetMappedPublicPort(RedisPort)}";

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync() => _container.DisposeAsync();
}
