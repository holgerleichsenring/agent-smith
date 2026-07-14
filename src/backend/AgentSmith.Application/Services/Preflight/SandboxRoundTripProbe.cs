using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Preflight;

/// <summary>
/// p0324: the CLI-side sandbox probe — a real spawn + exec round-trip through the
/// composition's ISandboxFactory (in-process for one-shot CLI runs, container-backed
/// where so composed). A trivial echo proves create, step transport and teardown all
/// work. Never throws.
/// </summary>
public sealed class SandboxRoundTripProbe(
    ISandboxFactory sandboxFactory,
    ILogger<SandboxRoundTripProbe> logger) : IPreflightSandboxProbe
{
    private const string ProbeImage = "agentsmith-preflight-probe";

    public string BackendLabel =>
        sandboxFactory.GetType().Name.Replace("SandboxFactory", "", StringComparison.Ordinal);

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var spec = new SandboxSpec(ProbeImage, ResourceLimits.LightProfile, TimeoutSeconds: 60);
            await using var sandbox = await sandboxFactory.CreateAsync(spec, cancellationToken);
            var step = new Step(
                Step.CurrentSchemaVersion, Guid.NewGuid(),
                Command: "echo", Args: ["preflight"], TimeoutSeconds: 30);
            var result = await sandbox.RunStepAsync(step, progress: null, cancellationToken);
            return result.ExitCode == 0
                ? ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds)
                : ConnectionProbeResult.Unreachable(
                    stopwatch.ElapsedMilliseconds,
                    $"probe step exited {result.ExitCode}: {result.ErrorMessage ?? "no error detail"}");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Sandbox round-trip probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }
}
