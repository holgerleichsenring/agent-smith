using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Preflight;

/// <summary>
/// p0355: WARNs at startup when the orchestrator's memory ceiling (the cgroup
/// limit .NET reports as total available memory) is below a sane floor. A
/// 256Mi server pod OOMKills under normal load — each restart reaps the
/// in-flight run, truncates the durable event trail, and orphans the run's
/// sandbox pods. This check names that misconfiguration on day one instead of
/// letting it surface as mysterious "cancelled by operator" runs.
/// </summary>
public sealed class ServerMemoryFloorCheck(Func<long> availableMemoryBytes) : IPreflightCheck
{
    /// <summary>Sane floor for the orchestrator: 512Mi (recommended request;
    /// limit 1-1.5Gi — see docs/host-it/kubernetes.md).</summary>
    internal const long FloorBytes = 512L * 1024 * 1024;

    private const long BytesPerMi = 1024 * 1024;

    public string Name => "server-memory-floor";

    public string Category => "infra";

    public Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Evaluate());

    private PreflightCheckResult Evaluate()
    {
        var available = availableMemoryBytes();
        var availableMi = available / BytesPerMi;
        if (available < FloorBytes)
            return PreflightCheckResult.Fail(
                $"memory ceiling is {availableMi}Mi — below the {FloorBytes / BytesPerMi}Mi floor for the orchestrator",
                "Raise the server pod's memory (request ~512Mi, limit 1-1.5Gi) — an under-provisioned limit "
                + "OOMKills the server, which reaps in-flight runs, truncates event trails, and orphans "
                + "sandbox pods. Mind the namespace ResourceQuota, which counts this limit too.");
        return PreflightCheckResult.Pass(
            $"memory ceiling {availableMi}Mi (floor {FloorBytes / BytesPerMi}Mi)");
    }
}
