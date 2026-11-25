using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-31dce: before a merge-green run merges a pull request, its base is read from the host — the page's 'ready'
/// and the CI watch's green may be stale, and a stacked pull request merged into a prerequisite's branch is not a landing.
/// Refused when the base is not the repo's configured default branch, or, with none configured, when it is an agent-smith
/// work branch, and when a hosted repository's base cannot be read (the providers answer null on a failed read). A local
/// repository has no pull request and no base. 2026-11-24-f9c5c: the merge's series branch is a target it accepts — a series
/// ticket lands there.
/// </summary>
public sealed class PullRequestBaseGuard(ISourceProviderFactory sources)
{
    public async Task<string?> RefusalAsync(RepoConnection repo, string prUrl, CancellationToken ct, string? seriesBranch = null)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (repo.Type == RepoType.Local) return null;
        if (await sources.Create(repo).ReadPullRequestBaseAsync(prUrl, ct) is not { } current)
            return Unread;
        if (seriesBranch is not null && string.Equals(current, seriesBranch, StringComparison.Ordinal)) return null;
        var main = string.IsNullOrWhiteSpace(repo.DefaultBranch) ? null : repo.DefaultBranch;
        var wrong = main is null ? WorkBranches.Owns(current) : !string.Equals(current, main, StringComparison.Ordinal);
        if (!wrong) return null;
        var onto = seriesBranch is null ? string.Empty : $"the series branch '{seriesBranch}' or "; // 2026-11-25-96aeb
        return $"targets '{current}', not {onto}the default branch{(main is null ? string.Empty : $" '{main}'")}, and merging it would "
            + "land it there rather than on the default branch; left open until it is retargeted";
    }

    /// <summary>2026-11-25-96aeb: the refusal when the host could not say the base — one a judged merge passes.</summary>
    public const string Unread = "its base could not be read from the host, so it is not known to target the default branch; left open";

    /// <summary>2026-11-25-96aeb: the refusal is the unreadable base's.</summary>
    public static bool IsUnread(string refusal) => string.Equals(refusal, Unread, StringComparison.Ordinal);
}
