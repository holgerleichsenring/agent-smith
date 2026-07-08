using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Posts comments on pull requests / merge requests.
/// Implemented by source providers that support PR commenting (GitHub, GitLab, AzDO).
/// <para>p0167c adds the typed review-batch surface: PostReviewBatchAsync posts
/// a compiled <see cref="PrReviewSummary"/> (inline comments on new-side file
/// lines + one top-level summary), DeleteCommentsByMarkerAsync removes the
/// previous run's marker-tagged comments so a re-review on PR-synchronize is
/// idempotent. PostCommentAsync stays for the plain-text dialogue trail.</para>
/// </summary>
public interface IPrCommentProvider
{
    Task PostCommentAsync(string prIdentifier, string markdown, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts the compiled review: every inline comment anchored at its
    /// file + new-side line span, plus the top-level summary comment.
    /// Platforms with a batch API (GitHub review-comments) submit all inline
    /// comments in one call; the others post per-line comments individually.
    /// </summary>
    Task PostReviewBatchAsync(string prIdentifier, PrReviewSummary review, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every PR comment whose body starts with <paramref name="markerPrefix"/>
    /// (the agentsmith pr-review marker left by a previous run). Returns the
    /// number of comments deleted.
    /// </summary>
    Task<int> DeleteCommentsByMarkerAsync(string prIdentifier, string markerPrefix, CancellationToken cancellationToken = default);
}
