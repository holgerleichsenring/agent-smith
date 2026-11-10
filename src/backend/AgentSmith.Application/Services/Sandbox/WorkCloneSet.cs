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

    /// <summary>The repository's clone; <paramref name="fullHistory"/> counts only on the first call.</summary>
    public async Task<ISandbox> ForAsync(
        string repository, bool fullHistory, IReadOnlyDictionary<string, ISandbox> heldScopes, CancellationToken ct)
    {
        if (_clones.TryGetValue(repository, out var held)) return held;
        await _gate.WaitAsync(ct);
        try
        {
            if (_clones.TryGetValue(repository, out held)) return held;
            var repo = repos.FirstOrDefault(r => string.Equals(r.Name, repository, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException(
                           $"'{repository}' is not a repository of this turn: [{string.Join(", ", Repositories)}].");
            if (!heldScopes.TryGetValue(repo.Name, out var scope) || scope is not ISourceScopeSandbox read)
                throw new InvalidOperationException($"'{repo.Name}' has no read scope this turn, so no commit to clone at.");
            var sha = await read.MaterializeAsync(ct);
            if (string.IsNullOrWhiteSpace(sha))
                throw new InvalidOperationException($"The read scope of '{repo.Name}' reports no commit; nothing is cloned.");
            return _clones[repo.Name] = await opener.OpenAsync(project, repo, sha, fullHistory, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var clone in _clones.Values) await clone.DisposeAsync();
        _clones.Clear();
        _gate.Dispose();
    }
}
