using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-24-5ee0: sets the token's own Active or Pending pull-request threads whose first comment starts with a marker to
/// Fixed — the state Azure's "comments must be resolved" policy accepts. A thread a stranger opened is left, since a marker is
/// text anyone can paste (as 2026-10-08-f147's delete reads it); so is one the token cannot be identified for.
/// </summary>
public sealed class AzureReposMarkedThreads(
    Func<CancellationToken, Task<GitHttpClient>> git, Func<CancellationToken, Task<string?>> self,
    string project, string repoName, ILogger logger)
{
    public async Task<int> ResolveAsync(string prIdentifier, string markerPrefix, CancellationToken ct)
    {
        var prId = int.Parse(prIdentifier, System.Globalization.CultureInfo.InvariantCulture);
        if (await self(ct) is not { } me) return 0;
        var client = await git(ct);
        var threads = await client.GetThreadsAsync(project, repoName, prId, cancellationToken: ct);
        var resolved = 0;
        foreach (var thread in threads.Where(t => IsOpenMarked(t, me, markerPrefix)))
            if (await FixAsync(client, prId, thread.Id, ct)) resolved++;
        if (resolved > 0) logger.LogInformation("PR !{PrId}: {Count} marked thread(s) resolved", prId, resolved);
        return resolved;
    }

    // One thread Azure refuses leaves the others to be resolved.
    private async Task<bool> FixAsync(GitHttpClient client, int prId, int threadId, CancellationToken ct)
    {
        try
        {
            await client.UpdateThreadAsync(new GitPullRequestCommentThread { Status = CommentThreadStatus.Fixed },
                project, repoName, prId, threadId, cancellationToken: ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "PR !{PrId}: marked thread {ThreadId} was not resolved", prId, threadId);
            return false;
        }
    }

    private static bool IsOpenMarked(GitPullRequestCommentThread thread, string me, string markerPrefix) =>
        thread.IsDeleted != true && thread.Status is CommentThreadStatus.Active or CommentThreadStatus.Pending
        && (thread.Comments ?? []).OrderBy(c => c.Id).FirstOrDefault() is { } first
        && string.Equals(first.Author?.Id, me, StringComparison.OrdinalIgnoreCase)
        && first.Content?.StartsWith(markerPrefix, StringComparison.Ordinal) == true;
}
