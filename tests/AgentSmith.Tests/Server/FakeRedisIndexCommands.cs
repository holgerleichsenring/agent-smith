using AgentSmith.Server.Services.Events;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-02-5ab2c: the two atomic index commands over <see cref="FakeRedisState"/> — the
/// publisher's RunFinished transaction (queued, applied together on execute, as MULTI/EXEC)
/// and the re-seed script, modelled by its contract: add each id unless the recent list holds
/// it. The Lua itself runs against a real Redis in the docker tier.
/// </summary>
internal static class FakeRedisIndexCommands
{
    public static void Setup(Mock<IDatabase> db, FakeRedisState state)
    {
        db.Setup(d => d.CreateTransaction(It.IsAny<object?>())).Returns(() => Transaction(state));
        db.Setup(d => d.ScriptEvaluateAsync(ActiveRunSetReseeder.ReseedScript,
                It.IsAny<RedisKey[]?>(), It.IsAny<RedisValue[]?>(), It.IsAny<CommandFlags>()))
            .Returns((string _, RedisKey[]? keys, RedisValue[]? ids, CommandFlags _) =>
                Task.FromResult(RedisResult.Create(state.AddUnlessListed(
                    keys![0].ToString(), keys[1].ToString(), ids!.Select(i => i.ToString())))));
    }

    private static ITransaction Transaction(FakeRedisState state)
    {
        var queued = new List<Action>();
        var tx = new Mock<ITransaction>();
        tx.Setup(t => t.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey k, RedisValue m, CommandFlags _) =>
            {
                queued.Add(() => state.SetRemove(k.ToString(), m.ToString()));
                return Task.FromResult(true);
            });
        tx.Setup(t => t.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey k, RedisValue v, When _, CommandFlags _) =>
            {
                queued.Add(() => state.ListLeftPush(k.ToString(), v.ToString()));
                return Task.FromResult(1L);
            });
        tx.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(() =>
        {
            state.Atomically(queued);
            return true;
        });
        return tx.Object;
    }
}
