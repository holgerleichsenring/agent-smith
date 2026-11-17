using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-16-f3fbc: one pass's readiness answers — each repository is probed once on its base branch
/// (default_branch, else the host's default), however many stuck tickets name it.
/// </summary>
public sealed class RepoReadinessPass(RepoReadiness readiness)
{
    private readonly Dictionary<string, Task<RemoteBootstrapVerdict>> _verdicts = new(StringComparer.Ordinal);

    public Task<RemoteBootstrapVerdict> ReadAsync(RepoConnection repo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (_verdicts.TryGetValue(repo.Name, out var known)) return known;
        var baseBranch = string.IsNullOrWhiteSpace(repo.DefaultBranch) ? null : new BranchName(repo.DefaultBranch);
        var read = readiness.ReadAsync(repo, baseBranch, null, ct);
        _verdicts[repo.Name] = read;
        return read;
    }
}
