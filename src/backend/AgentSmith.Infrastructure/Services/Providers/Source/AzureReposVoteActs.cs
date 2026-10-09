using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9c: Azure DevOps' standing "Wait for author" votes. A vote leaves a system thread
/// whose properties say CodeReviewThreadType VoteUpdate and CodeReviewVoteResult -5; only the voter's
/// NEWEST such thread counts, and only while the reviewer list still shows that voter at -5 — a reset
/// vote, or our auto-complete's +10, leaves no standing act. The pull request's creator is skipped.
/// </summary>
public static class AzureReposVoteActs
{
    private const short WaitForAuthor = -5;

    public static IReadOnlyList<PrReviewNote> Standing(
        IEnumerable<GitPullRequestCommentThread> threads, IEnumerable<IdentityRefWithVote> reviewers,
        string? creatorId, string repoUrl, string repositoryId, string projectId)
    {
        var atMinusFive = reviewers.Where(r => r.Vote == WaitForAuthor && r.Id is not null)
            .Select(r => r.Id!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. threads.Where(t => t.IsDeleted != true && IsWaitForAuthorVote(t))
            .Select(t => (Voter: t.Comments?.FirstOrDefault()?.Author, t.PublishedDate))
            .Where(v => v.Voter?.Id is { } id && atMinusFive.Contains(id) && !string.Equals(id, creatorId, StringComparison.OrdinalIgnoreCase))
            .GroupBy(v => v.Voter!.Id!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MaxBy(v => v.PublishedDate))
            .Select(v => new PrReviewNote(
                new PrCommentAuthor(repoUrl, repositoryId, v.Voter!.Id!, v.Voter.UniqueName ?? v.Voter.Id!) { ProjectId = projectId },
                false, false, new DateTimeOffset(DateTime.SpecifyKind(v.PublishedDate, DateTimeKind.Utc)), string.Empty))];
    }

    private static bool IsWaitForAuthorVote(GitPullRequestCommentThread thread) =>
        Property(thread, "CodeReviewThreadType") == "VoteUpdate" && Property(thread, "CodeReviewVoteResult") == "-5";

    // The REST shape is {"$type": ..., "$value": ...}; the SDK may hand back the raw value or that object.
    private static string? Property(GitPullRequestCommentThread thread, string name)
    {
        if (thread.Properties is null || !thread.Properties.TryGetValue(name, out var value) || value is null) return null;
        if (value is string text) return text;
        var token = Newtonsoft.Json.Linq.JToken.FromObject(value);
        return token.Type == Newtonsoft.Json.Linq.JTokenType.Object ? token["$value"]?.ToString() : token.ToString();
    }
}
