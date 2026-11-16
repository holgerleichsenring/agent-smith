using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-16-f3fba: whether a repository is ready for a run, asked of the host before any sandbox. The
/// base the run starts from is read, and the run's own branch when it already exists — an existing
/// ticket branch gets its base merged at checkout, so either one carrying the bootstrap is enough.
/// Ready when either is Complete, Incomplete when either says so, otherwise Unknown: an absent branch
/// or an unreachable host is never proof that the repository is uninitialised.
/// </summary>
public sealed class RepoReadiness(
    RemoteBootstrapProbe probe, ISourceProviderFactory sources, ILogger<RepoReadiness> logger)
{
    /// <param name="baseBranch">The base; null reads the host's default branch.</param>
    /// <param name="runBranch">The run's branch; read only when the host has it.</param>
    public async Task<RemoteBootstrapVerdict> ReadAsync(
        RepoConnection repo, BranchName? baseBranch, BranchName? runBranch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var onBase = await probe.ProbeAsync(repo, baseBranch, ct);
        if (onBase.State == RemoteBootstrapState.Complete) return onBase;
        var onRun = runBranch is not null && runBranch != baseBranch && await ExistsAsync(repo, runBranch, ct)
            ? await probe.ProbeAsync(repo, runBranch, ct)
            : null;
        if (onRun?.State == RemoteBootstrapState.Complete) return onRun;
        if (onBase.State == RemoteBootstrapState.Incomplete) return onBase;
        return onRun?.State == RemoteBootstrapState.Incomplete ? onRun : onBase;
    }

    private async Task<bool> ExistsAsync(RepoConnection repo, BranchName branch, CancellationToken ct)
    {
        if (!repo.HasLocation) return false;
        try
        {
            return await sources.Create(repo).BranchExistsAsync(branch, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not ask whether {Branch} exists on {Repo}; the base answers alone.",
                branch.Value, repo.Name);
            return false;
        }
    }
}
