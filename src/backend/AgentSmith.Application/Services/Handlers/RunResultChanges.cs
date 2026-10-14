using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-3890b: which files a run changed, as result.md reports them. A pipeline that
/// commits its work (<see cref="WorkBranchPersistPolicy.IntendedToChangeCode"/>) is answered by
/// its branch — every sandbox of a repository, unioned, because earlier master passes committed
/// work the last pass's tool writes never saw. Any other pipeline's branch is someone else's, so
/// its tool writes stay the record. A sandbox whose branch cannot be read falls back to its
/// repository's share of the tool writes and says so.
/// </summary>
public sealed class RunResultChanges(
    BranchChangedFiles branchFiles, SandboxTargets sandboxTargets, ILogger<RunResultChanges> logger)
{
    public async Task<RunResultChangeSet> ResolveAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection>? repos,
        IReadOnlyList<CodeChange> toolWrites, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var fromBranch = ReadsTheBranch(pipeline);
        var multiRepo = repos is { Count: > 1 };
        var perRepo = new Dictionary<string, IReadOnlyList<CodeChange>>(StringComparer.Ordinal);
        var notes = new List<string>();
        foreach (var (name, sandboxes, share) in Targets(pipeline, repos, toolWrites))
            perRepo[name] = fromBranch
                ? await FromBranchAsync(pipeline, name, multiRepo, sandboxes, share, notes, ct)
                : share;
        return new RunResultChangeSet(perRepo, Coverage(perRepo), notes);
    }

    private static bool ReadsTheBranch(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.PipelineName, out var name)
        && !string.IsNullOrWhiteSpace(name)
        && PipelinePresets.TryResolve(name) is { } commands
        && WorkBranchPersistPolicy.IntendedToChangeCode(commands);

    private IEnumerable<(string Name, IReadOnlyList<KeyValuePair<string, ISandbox>> Sandboxes,
        IReadOnlyList<CodeChange> Share)> Targets(
        PipelineContext pipeline, IReadOnlyList<RepoConnection>? repos, IReadOnlyList<CodeChange> toolWrites)
    {
        if (repos is null)
        {
            yield return (RunResultChangeSet.NoRepository, Legacy(pipeline), toolWrites);
            yield break;
        }
        foreach (var repo in repos)
        {
            var sandboxes = sandboxTargets.SandboxesForRepo(pipeline, repo);
            var share = repos.Count == 1
                ? toolWrites
                : toolWrites.Where(c => c.Path.ToString().StartsWith(repo.Name + "/", StringComparison.Ordinal)).ToList();
            yield return (repo.Name, sandboxes.Count > 0 ? sandboxes : Legacy(pipeline), share);
        }
    }

    private static IReadOnlyList<KeyValuePair<string, ISandbox>> Legacy(PipelineContext pipeline) =>
        pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) && sandbox is not null
            ? [new(ContextKeys.Sandbox, sandbox)]
            : [];

    private async Task<IReadOnlyList<CodeChange>> FromBranchAsync(
        PipelineContext pipeline, string repoName, bool multiRepo,
        IReadOnlyList<KeyValuePair<string, ISandbox>> sandboxes, IReadOnlyList<CodeChange> share,
        List<string> notes, CancellationToken ct)
    {
        var union = new List<CodeChange>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var files = await TryReadAsync(sandbox, DeliveryBasis.For(pipeline), ct);
            if (files is null)
                notes.Add($"[changed-files] the branch in sandbox '{key}' could not be read; its files "
                    + "are this run's tool writes instead (diagnostic, not a gate)");
            union.AddRange(files?.Select(f => multiRepo ? Under(repoName, f) : f) ?? share);
        }
        return union.DistinctBy(c => c.Path.ToString(), StringComparer.Ordinal).ToList();
    }

    private async Task<IReadOnlyList<CodeChange>?> TryReadAsync(
        ISandbox sandbox, DeliveryBasis basis, CancellationToken ct)
    {
        try
        {
            return await branchFiles.ReadAsync(sandbox, basis, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The branch in sandbox {JobId} could not be read", sandbox.JobId);
            return null;
        }
    }

    private static List<CodeChange> Coverage(Dictionary<string, IReadOnlyList<CodeChange>> perRepo) =>
        perRepo.SelectMany(kv => kv.Value.Select(c => Prefixed(c, kv.Key))).ToList();

    /// <summary>A tool write in a multi-repo run already carries its repository; a branch
    /// file never does.</summary>
    private static CodeChange Prefixed(CodeChange change, string repoName) =>
        repoName.Length == 0 || change.Path.ToString().StartsWith(repoName + "/", StringComparison.Ordinal)
            ? change
            : Under(repoName, change);

    private static CodeChange Under(string repoName, CodeChange change) =>
        new(new FilePath($"{repoName}/{change.Path}"), change.Content, change.ChangeType);
}
