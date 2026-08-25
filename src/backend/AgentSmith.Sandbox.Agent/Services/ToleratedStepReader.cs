using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// Reads the next step off the bus and answers null rather than throwing, so nothing the
/// wire can carry ends this process.
/// <para>
/// p0396 established the first half: a transient Redis hiccup during the idle wait escaped
/// the loop, exited the container with code 3, and reached the operator as a cancelled run
/// with "sandbox vanished". 2026-08-25-0d01 adds the second: a message this build cannot
/// parse at all fails in exactly the same place, with exactly the same ending. A step that
/// merely names a KIND this build does not know never reaches here — the wire's tolerant
/// enum converter turns that into <see cref="StepKind.Unknown"/>, which the agent answers
/// with a result. What is left for this reader is a message that is not a step at all.
/// </para>
/// <para>
/// Both faults are reported to the caller as SILENCE, which the job loop already counts as
/// an idle cycle — so its idle budget stays the only way the loop ends on its own, and a
/// bus that stays broken exhausts the same budget an idle server does.
/// </para>
/// </summary>
internal sealed class ToleratedStepReader(IRedisJobBus bus, ILogger<ToleratedStepReader> logger)
{
    public async Task<Step?> NextAsync(string jobId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await bus.WaitForStepAsync(jobId, timeout, cancellationToken);
        }
        catch (Exception ex) when (ex is RedisTimeoutException or RedisConnectionException)
        {
            logger.LogWarning(ex,
                "Transient Redis error while waiting for a step — counting as an idle cycle");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "A message on this job's input queue could not be read as a step by an agent "
                + "speaking wire protocol {Window} — discarding it and counting an idle cycle",
                WireProtocol.Window);
            return null;
        }
    }
}
