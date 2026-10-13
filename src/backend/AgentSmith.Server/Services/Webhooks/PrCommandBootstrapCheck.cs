using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-13-0b82b: a PR command for a pipeline that needs a bootstrapped repository is checked
/// against the pull request's branch before a run exists. Only an incomplete answer refuses; an
/// unknown one (absent branch, unreachable host) lets the run start and its own gate decide. A fork's
/// branch is not in this repository, so it is not read.
/// </summary>
public sealed class PrCommandBootstrapCheck(RemoteBootstrapProbe probe)
{
    /// <summary>The answer to the command when the branch is not bootstrapped, otherwise null.</summary>
    public async Task<string?> RefusalAsync(
        RepoConnection repo, string pipeline, IReadOnlyDictionary<string, object> context, bool sameRepository, CancellationToken ct)
    {
        if (!sameRepository || !NeedsBootstrap(pipeline)) return null;
        if (!context.TryGetValue(ContextKeys.CheckoutBranch, out var value) || value is not string branch || string.IsNullOrWhiteSpace(branch))
            return null;
        var verdict = await probe.ProbeAsync(repo, new BranchName(branch), ct);
        return verdict.State == RemoteBootstrapState.Incomplete ? PrCommandTexts.NotBootstrapped(branch, verdict.Missing) : null;
    }

    private static bool NeedsBootstrap(string pipeline) =>
        PipelinePresets.TryResolve(pipeline)?.Contains(CommandNames.BootstrapGate) == true;
}
