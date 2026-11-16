using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-13-0b82b: a PR command for a pipeline that needs a bootstrapped repository is checked
/// against the pull request before a run exists. Only an incomplete answer refuses; an unknown one
/// (absent branch, unreachable host) lets the run start and its own preflight decide. A fork's
/// branch is not in this repository, so it is not read.
/// <para>
/// 2026-11-16-f3fba: asked through <see cref="RepoReadiness"/> — the repository's default branch and
/// the pull request's branch, ready when either carries the bootstrap — for every preset that holds
/// RunConfigPreflight, the step that now carries the run's own bootstrap check.
/// </para>
/// </summary>
public sealed class PrCommandBootstrapCheck(RepoReadiness readiness)
{
    /// <summary>The answer to the command when the branch is not bootstrapped, otherwise null.</summary>
    public async Task<string?> RefusalAsync(
        RepoConnection repo, string pipeline, IReadOnlyDictionary<string, object> context, bool sameRepository, CancellationToken ct)
    {
        if (!sameRepository || !NeedsBootstrap(pipeline)) return null;
        if (!context.TryGetValue(ContextKeys.CheckoutBranch, out var value) || value is not string branch || string.IsNullOrWhiteSpace(branch))
            return null;
        var baseBranch = string.IsNullOrWhiteSpace(repo.DefaultBranch) ? null : new BranchName(repo.DefaultBranch);
        var verdict = await readiness.ReadAsync(repo, baseBranch, new BranchName(branch), ct);
        return verdict.State == RemoteBootstrapState.Incomplete ? PrCommandTexts.NotBootstrapped(branch, verdict.Missing) : null;
    }

    private static bool NeedsBootstrap(string pipeline) =>
        PipelinePresets.TryResolve(pipeline)?.Contains(CommandNames.RunConfigPreflight) == true;
}
