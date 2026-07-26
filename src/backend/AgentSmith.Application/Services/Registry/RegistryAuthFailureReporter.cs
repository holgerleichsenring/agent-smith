using AgentSmith.Contracts.Decisions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// p0375: the LOUD path for generic registry-auth staging failures (the p0300b
/// class). Every gap — LLM error, unparseable output, guard rejection,
/// unmatched placeholder — logs a WARN and records a decision line on the run
/// via the existing decisions channel, so the master and the operator see
/// "registry auth NOT staged for host X" BEFORE a build error masquerades as a
/// missing package. Fail-soft: the run proceeds, but visibly.
/// </summary>
public sealed class RegistryAuthFailureReporter(
    IDecisionLogger decisionLogger,
    ILogger<RegistryAuthFailureReporter> logger)
{
    private const string SourceLabel = "SetupRegistryAuth";

    public async Task ReportAsync(string repoKey, string host, string reason, CancellationToken ct)
    {
        logger.LogWarning(
            "{Repo}: registry auth NOT staged for host {Host}: {Reason}", repoKey, host, reason);
        await decisionLogger.LogAsync(
            repoPath: null, DecisionCategory.Tooling,
            $"registry auth NOT staged for host {host}: {reason}", ct, SourceLabel);
    }
}
