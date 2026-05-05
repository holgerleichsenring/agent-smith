using Testcontainers.Redis;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public sealed class RedisJobBusFixture : IAsyncLifetime
{
    public RedisContainer Container { get; } = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(RedisJobBusCollection))]
public sealed class RedisJobBusCollection : ICollectionFixture<RedisJobBusFixture>
{
}
