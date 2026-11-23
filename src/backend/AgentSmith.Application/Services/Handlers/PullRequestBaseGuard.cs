using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-31dce: before a merge-green run merges a pull request, its base is read from the host — the page's 'ready'
/// and the CI watch's green may be stale, and a stacked pull request merged into a prerequisite's branch is not a landing.
/// Refused when the base is not the repo's configured default branch, or, with none configured, when it is an agent-smith
/// work branch, and when a hosted repository's base cannot be read (the providers answer null on a failed read). A local
/// repository has no pull request and no base.
/// </summary>
public sealed class PullRequestBaseGuard(ISourceProviderFactory sources)
{
    public async Task<string?> RefusalAsync(RepoConnection repo, string prUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (repo.Type == RepoType.Local) return null;
        if (await sources.Create(repo).ReadPullRequestBaseAsync(prUrl, ct) is not { } current)
            return "its base could not be read from the host, so it is not known to target the default branch; left open";
        var main = string.IsNullOrWhiteSpace(repo.DefaultBranch) ? null : repo.DefaultBranch;
        var wrong = main is null ? WorkBranches.Owns(current) : !string.Equals(current, main, StringComparison.Ordinal);
        if (!wrong) return null;
        return $"targets '{current}', not the default branch{(main is null ? string.Empty : $" '{main}'")}, and merging it would "
            + "land it there rather than on the default branch; left open until it is moved";
    }
}
