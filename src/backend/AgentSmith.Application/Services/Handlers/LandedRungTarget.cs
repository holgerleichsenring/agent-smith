using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-26-5201f: the base a stacked repository's pull request opens or moves onto at the run's end. The claim stamped a
/// prerequisite's branch (<see cref="PullRequestTargets"/>), but the prerequisite may have merged there while the run worked.
/// Asked by the claim's own rule (<see cref="IPreviousAttemptReader.PullRequestReposAsync"/>: a repo whose pull request merged is
/// no base), a rung that is no base any more targets the series branch, else the repository's configured default branch —
/// never a branch that already merged. A read that fails keeps the stamp.
/// </summary>
public sealed class LandedRungTarget(IPreviousAttemptReader attempts, ILogger<LandedRungTarget> logger)
{
    public async Task<BranchName?> ForAsync(PipelineContext pipeline, string project, RepoConnection repo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repo);
        var rung = PullRequestTargets.For(pipeline, repo.Name);
        if (rung is null || RunParentTicket.Stacked(pipeline)?.For(repo.Name) is not { } prerequisite) return rung;
        try
        {
            var standing = await attempts.PullRequestReposAsync(project, [prerequisite], ct);
            if (standing.TryGetValue(prerequisite, out var repos) && repos.Contains(repo.Name)) return rung;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Repo}: whether the stacked base {Rung} still stands could not be read; it stays the target", repo.Name, rung.Value);
            return rung;
        }
        var landed = RunParentTicket.Series(pipeline) ?? repo.DefaultBranch;
        logger.LogInformation("{Repo}: the stacked base {Rung} merged there; the pull request targets {Base}",
            repo.Name, rung.Value, landed ?? "the host's default branch");
        return string.IsNullOrWhiteSpace(landed) ? null : new BranchName(landed);
    }
}
