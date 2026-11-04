namespace TickestPristine.IntegrationTests.Caching.StampAge;

[CollectionDefinition(nameof(StampAgePrototypeCollection), DisableParallelization = true)]
public sealed class StampAgePrototypeCollection
    : ICollectionFixture<IntegrationTestWebAppFactory>, ICollectionFixture<RedisContainerFixture>;
