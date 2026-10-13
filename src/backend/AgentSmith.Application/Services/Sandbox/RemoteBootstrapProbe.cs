using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-13-0b82b: the bootstrap gate's question asked of the host, before a run exists. The
/// contexts are the ones a run would discover (<see cref="ISandboxLanguageResolver.ListContextsAsync"/>,
/// none being the synthetic 'default'); each is then read for the same files
/// <c>BootstrapContextProbe</c> reads in the sandbox, on the probed branch, so this answer and a run's
/// refusal never disagree. Paths are repository-relative — the sandbox paths start at /work.
/// </summary>
public sealed class RemoteBootstrapProbe(
    ISourceProviderFactory sources, ISandboxLanguageResolver contexts, ILogger<RemoteBootstrapProbe> logger)
{
    private const string SyntheticContext = "default";

    /// <summary>The bootstrap of <paramref name="repo"/> on <paramref name="branch"/>, or on its default branch when null.</summary>
    public async Task<RemoteBootstrapVerdict> ProbeAsync(RepoConnection repo, BranchName? branch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (!repo.HasLocation) return RemoteBootstrapVerdict.Unknown("the repository names no url or path");
        try
        {
            var provider = sources.Create(repo);
            if (branch is not null && !await provider.BranchExistsAsync(branch, ct))
                return RemoteBootstrapVerdict.Unknown($"branch '{branch.Value}' is not on the remote");
            var listing = await contexts.ListContextsAsync(repo, Candidates(branch), ct);
            if (listing.UnreadableReason is { } unreadable) return RemoteBootstrapVerdict.Unknown(unreadable);
            var names = listing.Contexts.Count == 0 ? [SyntheticContext] : listing.Contexts.Select(c => c.ContextName).ToList();
            var results = new List<ContextProbeResult>(names.Count);
            foreach (var name in names) results.Add(await ProbeOneAsync(provider, branch, name, ct));
            return RemoteBootstrapVerdict.Of(results);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Bootstrap read of {Repo} failed; the answer is unknown.", repo.Name);
            return RemoteBootstrapVerdict.Unknown(ex.Message);
        }
    }

    private static ContextReadBranches Candidates(BranchName? branch) =>
        branch is null ? ContextReadBranches.None : ContextReadBranches.ForBranch(branch);

    private static async Task<ContextProbeResult> ProbeOneAsync(
        ISourceProvider provider, BranchName? branch, string context, CancellationToken ct)
    {
        var dir = $"{ProjectMetaPaths.Contexts}/{context}";
        var contextYaml = await ExistsAsync(provider, branch, $"{dir}/{ProjectMetaPaths.ContextYamlFile}", ct);
        var principles = await ExistsAsync(provider, branch, $"{dir}/{ProjectMetaPaths.PrinciplesFile}", ct);
        var retired = !principles && await ExistsAsync(provider, branch, $"{dir}/{ProjectMetaPaths.RetiredPrinciplesFile}", ct);
        return new ContextProbeResult(context, contextYaml, principles, retired);
    }

    private static async Task<bool> ExistsAsync(ISourceProvider provider, BranchName? branch, string path, CancellationToken ct) =>
        (branch is null ? await provider.TryReadFileAsync(path, ct) : await provider.TryReadFileAsync(path, branch, ct)) is not null;
}
