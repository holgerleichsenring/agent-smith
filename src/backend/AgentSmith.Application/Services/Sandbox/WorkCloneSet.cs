using System.Collections.Concurrent;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-10-4778: one design turn's work clones, opened lazily — the first run in a repository materialises the
/// held read scope, then clones at the commit it shows — and disposed when the turn ends, so the next turn starts
/// from a new clone and nothing a shell left behind survives it.
/// </summary>
public sealed class WorkCloneSet(
    WorkCloneOpener opener, ResolvedProject project, IReadOnlyList<RepoConnection> repos) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ISandbox> _clones = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public IReadOnlyList<string> Repositories => [.. repos.Select(r => r.Name)];

    public int Opened => _clones.Count;

    private readonly HashSet<string> _full = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The repository's clone. Asking for <paramref name="fullHistory"/> on a shallow clone gives it back
    /// and clones again in full: deepening it would be a credentialed step after the model's commands.</summary>
    public async Task<ISandbox> ForAsync(
        string repository, bool fullHistory, IReadOnlyDictionary<string, ISandbox> heldScopes, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_clones.TryGetValue(repository, out var held))
            {
                if (!fullHistory || _full.Contains(repository)) return held;
                _clones.TryRemove(repository, out _);
                await held.DisposeAsync();
            }
            var repo = repos.FirstOrDefault(r => string.Equals(r.Name, repository, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException(
                           $"'{repository}' is not a repository of this turn: [{string.Join(", ", Repositories)}].");
            if (!heldScopes.TryGetValue(repo.Name, out var scope) || scope is not ISourceScopeSandbox read)
                throw new InvalidOperationException($"'{repo.Name}' has no read scope this turn, so no commit to clone at.");
            var sha = await read.MaterializeAsync(ct);
            if (string.IsNullOrWhiteSpace(sha))
                throw new InvalidOperationException($"The read scope of '{repo.Name}' reports no commit; nothing is cloned.");
            var clone = await opener.OpenAsync(project, repo, sha, fullHistory, ct);
            if (fullHistory) _full.Add(repo.Name);
            return _clones[repo.Name] = clone;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Gives back every clone; one that throws stops none of the others, and the first failure is
    /// rethrown once all were tried.</summary>
    public async ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        foreach (var clone in _clones.Values)
        {
            try { await clone.DisposeAsync(); }
            catch (Exception ex) { failures.Add(ex); }
        }
        _clones.Clear();
        _full.Clear();
        _gate.Dispose();
        if (failures.Count > 0) throw new AggregateException("Work clones were not all given back.", failures);
    }
}
