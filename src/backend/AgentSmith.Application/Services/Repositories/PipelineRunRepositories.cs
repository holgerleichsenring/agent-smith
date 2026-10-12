using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Repositories;

/// <summary>
/// 2026-10-12-f5f7c: the run's repositories read off the pipeline on every call, so one that
/// ensure_repo_sandbox adds mid-run counts from then on. A repository's sandbox is the one
/// CommitAndPR commits from (the first it owns — the others are folded into it at commit time).
/// A code change is CommitAndPR's own test: a working-tree path outside the run record, or
/// code a checkpoint already pushed.
/// </summary>
public sealed class PipelineRunRepositories(
    PipelineContext pipeline,
    SandboxTargets sandboxTargets,
    ISandboxFileReaderFactory readerFactory,
    SandboxWorkingTreeReader workingTree) : IRunRepositories
{
    public IReadOnlyList<string> Names()
    {
        var sandboxes = Sandboxes();
        IEnumerable<string> names = Owners() is { } owners
            ? sandboxes.Keys.Select(k => owners.TryGetValue(k, out var o) ? o : null).OfType<string>()
            : ConfiguredRepos() is { } repos ? repos.Select(r => r.Name) : sandboxes.Keys;
        return [.. names.Where(n => !IsReadOnlyScope(n)).Distinct(StringComparer.Ordinal)];
    }

    public ISandboxFileReader? FilesOf(string repository) =>
        Owned(repository).FirstOrDefault().Value is { } sandbox ? readerFactory.Create(sandbox) : null;

    public async Task<bool> HasCodeChangeAsync(string repository, CancellationToken cancellationToken)
    {
        if (RunWorkCheckpointer.HasCheckpointedCode(pipeline, repository)) return true;
        var owned = Owned(repository).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        if (owned.Count == 0) return false;
        var paths = await workingTree.ChangedPathsAsync(owned, keyToRepo: null, cancellationToken);
        return paths.Any(p => !RunRecordPaths.IsRunRecordPath(p));
    }

    private IReadOnlyList<KeyValuePair<string, ISandbox>> Owned(string repository)
    {
        if (IsReadOnlyScope(repository)) return [];
        if (Owners() is null && ConfiguredRepos() is null)
            return Sandboxes().TryGetValue(repository, out var own) ? [new(repository, own)] : [];
        return sandboxTargets.SandboxesForRepo(pipeline, new RepoConnection { Name = repository });
    }

    private IReadOnlyDictionary<string, ISandbox> Sandboxes() =>
        pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var s) && s is not null
            ? s : new Dictionary<string, ISandbox>();

    private IReadOnlyDictionary<string, string>? Owners() =>
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var o) ? o : null;

    private IReadOnlyList<RepoConnection>? ConfiguredRepos() =>
        pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r) && r is { Count: > 0 } ? r : null;

    private static bool IsReadOnlyScope(string name) =>
        name.StartsWith(TemplateScopeName.Prefix, StringComparison.Ordinal)
        || name.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal);
}
