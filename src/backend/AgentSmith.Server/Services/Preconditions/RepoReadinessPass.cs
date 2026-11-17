using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-16-f3fbc: one pass's readiness answers — each repository is read once per run branch and parent, by
/// the rule the run's own bootstrap check applies (<see cref="RepoReadiness.ReadForRunAsync"/>), with the branch
/// and parent the stuck attempt recorded, however many stuck tickets name it.
/// </summary>
public sealed class RepoReadinessPass(RepoReadiness readiness)
{
    private readonly Dictionary<(string Repo, string? Branch, string? Parent), Task<RemoteBootstrapVerdict>> _verdicts = [];

    public Task<RemoteBootstrapVerdict> ReadAsync(RepoConnection repo, EndDetail detail, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(detail);
        var key = (repo.Name, detail.Branch, detail.ParentTicketId);
        if (_verdicts.TryGetValue(key, out var known)) return known;
        var branch = string.IsNullOrWhiteSpace(detail.Branch) ? null : new BranchName(detail.Branch);
        var read = readiness.ReadForRunAsync(repo, branch, detail.ParentTicketId, ct);
        _verdicts[key] = read;
        return read;
    }
}
