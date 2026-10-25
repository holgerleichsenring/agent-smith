using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-10-25-c5a7e: a Redis that records the ACL commands it is sent.</summary>
internal sealed class SandboxBusRedisDouble
{
    public Mock<IConnectionMultiplexer> Multiplexer { get; } = new();
    public Mock<IDatabase> Database { get; } = new();
    public List<object[]> AclCalls { get; } = [];

    public SandboxBusRedisDouble(Exception? refusal = null)
    {
        Multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Database.Object);
        var setup = Database.Setup(d => d.ExecuteAsync("ACL", It.IsAny<object[]>()));
        if (refusal is not null) setup.ThrowsAsync(refusal);
        else setup.Callback<string, object[]>((_, args) => AclCalls.Add(args))
            .ReturnsAsync(RedisResult.Create((RedisValue)"OK"));
    }

    public IEnumerable<object[]> Calls(string subcommand) =>
        AclCalls.Where(args => (string)args[0] == subcommand);
}
