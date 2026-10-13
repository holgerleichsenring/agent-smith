using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// The job's result list, read one result at a time. Split out of <see cref="SandboxRedisChannel"/>
/// (2026-10-13-3890d) when the channel took on serialising its callers.
/// </summary>
internal sealed class SandboxResultList(IDatabase database, string jobId, ILogger logger, IWireProtocolWatcher protocol)
{
    public async Task<StepResult?> TryPopAsync(Guid stepId)
    {
        var key = RedisKeys.ResultsKey(jobId);
        var value = await database.ListRightPopAsync(key);
        if (value.IsNull) return null;

        var result = JsonSerializer.Deserialize<StepResult>((string)value!, WireFormat.Json);
        if (result is null) return null;
        // 2026-08-25-0d01: every step produces exactly one result, so this is the one place
        // that hears from every agent that talks at all. What it declares here is the only
        // evidence about the protocol an agent actually speaks — a tag is not evidence.
        protocol.Observe(result.SchemaVersion);
        if (result.StepId == stepId) return result;

        logger.LogWarning("Discarded stale StepResult for step {StepId} (waiting for {WaitingFor})",
            result.StepId, stepId);
        return null;
    }
}
