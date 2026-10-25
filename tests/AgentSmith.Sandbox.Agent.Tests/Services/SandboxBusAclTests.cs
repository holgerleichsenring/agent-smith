using System.Text.Json;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// 2026-10-25-c5a7e: the measured half of the per-job ACL. The real agent bus connects to
/// redis:7 as the job's user under <see cref="SandboxBusAcl"/> and does everything a step
/// needs; a command on another job's key is refused. A command the client or the agent
/// needs and the list lacks fails here.
/// </summary>
[Collection(nameof(RedisJobBusCollection))]
[Trait("Category", "NeedsDocker")]
public sealed class SandboxBusAclTests(RedisJobBusFixture fixture)
{
    [Fact]
    public async Task SandboxBusAcl_AgentUnderItsUser_RunsAStepAndCannotTouchAnotherJob()
    {
        var jobId = $"job{Guid.NewGuid():N}";
        await using var admin = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var bus = await ConnectAs(admin, jobId);
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, "echo", ["hi"]);
        await admin.GetDatabase().ListLeftPushAsync(RedisKeys.InputKey(jobId), JsonSerializer.Serialize(step, WireFormat.Json));

        (await bus.WaitForStepAsync(jobId, TimeSpan.FromSeconds(5), CancellationToken.None)).Should().NotBeNull();
        await bus.PushResultAsync(jobId,
            new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 1.0, null), CancellationToken.None);
        await bus.IsRunActiveAsync("run-1", CancellationToken.None);
        var own = bus.Multiplexer.GetDatabase();
        await own.StringSetAsync(RedisKeys.HeartbeatKey(jobId), "beat", TimeSpan.FromSeconds(10));
        await own.KeyDeleteAsync(RedisKeys.HeartbeatKey(jobId));

        var foreign = () => own.ListRightPopAsync(RedisKeys.InputKey("another-job"));
        (await foreign.Should().ThrowAsync<RedisServerException>()).Which.Message.Should().Contain("NOPERM");
        bus.EnqueueEventsBatch(jobId, [new StepEvent(StepEvent.CurrentSchemaVersion, step.StepId,
            StepEventKind.Stdout, "hi", DateTimeOffset.UtcNow)]);
        await bus.DisposeAsync();
        (await admin.GetDatabase().StreamLengthAsync(RedisKeys.EventsKey(jobId))).Should().Be(1);
    }

    private async Task<RedisJobBus> ConnectAs(IConnectionMultiplexer admin, string jobId)
    {
        var password = Guid.NewGuid().ToString("N");
        object[] args = ["SETUSER", SandboxBusAcl.UserName(jobId), .. SandboxBusAcl.Rules(jobId, password)];
        await admin.GetDatabase().ExecuteAsync("ACL", args);
        var url = $"{fixture.ConnectionString},user={SandboxBusAcl.UserName(jobId)},password={password},tiebreaker=,configChannel=";
        return await RedisJobBus.ConnectAsync(url, NullLogger<RedisJobBus>.Instance, CancellationToken.None);
    }
}
