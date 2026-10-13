using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-13-9a59b: Azure DevOps vote threads — the system thread a vote leaves, whose properties say
/// CodeReviewThreadType VoteUpdate and CodeReviewVoteResult the vote. Per voter and vote only the
/// NEWEST such thread counts; its first comment is the vote's note.
/// </summary>
public static class AzureReposVoteThreads
{
    /// <summary>The newest vote thread of <paramref name="voterId"/> casting <paramref name="vote"/>, or null.</summary>
    public static (GitPullRequestCommentThread Thread, Comment Comment)? Newest(
        IEnumerable<GitPullRequestCommentThread> threads, string voterId, short vote) =>
        threads.Where(t => t.IsDeleted != true && VoteOf(t) == vote.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Select(t => (Thread: t, Comment: t.Comments?.FirstOrDefault()))
            .Where(v => v.Comment?.Author?.Id is { } id && string.Equals(id, voterId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v.Thread.PublishedDate)
            .Select(v => ((GitPullRequestCommentThread, Comment)?)(v.Thread, v.Comment!))
            .FirstOrDefault();

    /// <summary>The vote a VoteUpdate thread records, as text; null for any other thread.</summary>
    public static string? VoteOf(GitPullRequestCommentThread thread) =>
        Property(thread, "CodeReviewThreadType") == "VoteUpdate" ? Property(thread, "CodeReviewVoteResult") : null;

    // The REST shape is {"$type": ..., "$value": ...}; the SDK may hand back the raw value or that object.
    private static string? Property(GitPullRequestCommentThread thread, string name)
    {
        if (thread.Properties is null || !thread.Properties.TryGetValue(name, out var value) || value is null) return null;
        if (value is string text) return text;
        var token = Newtonsoft.Json.Linq.JToken.FromObject(value);
        return token.Type == Newtonsoft.Json.Linq.JTokenType.Object ? token["$value"]?.ToString() : token.ToString();
    }
}
