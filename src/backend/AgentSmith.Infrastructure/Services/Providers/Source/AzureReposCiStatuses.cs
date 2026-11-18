using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: the statuses an external CI posted on a pull request, as checks — only those of its latest
/// iteration (or of no iteration), the newest per context, so a status about an earlier push never decides the head.
/// </summary>
public static class AzureReposCiStatuses
{
    public static async Task<IReadOnlyList<AzureReposCiItem>> ReadAsync(
        GitHttpClient git, string project, string repoName, int prId, CancellationToken ct)
    {
        var iterations = await git.GetPullRequestIterationsAsync(project, repoName, prId, cancellationToken: ct);
        var latest = iterations.Count == 0 ? (int?)null : iterations.Max(i => i.Id);
        var statuses = await git.GetPullRequestStatusesAsync(project, repoName, prId, cancellationToken: ct);
        return [.. statuses.Where(s => s.IterationId is null || s.IterationId == latest)
            .Where(s => s.State != GitStatusState.NotApplicable)
            .GroupBy(Name).Select(g => g.MaxBy(s => s.Id)!).Select(Item)];
    }

    private static string Name(GitPullRequestStatus status) =>
        string.Join("/", new[] { status.Context?.Genre, status.Context?.Name }.Where(p => !string.IsNullOrEmpty(p)));

    private static AzureReposCiItem Item(GitPullRequestStatus status)
    {
        var outcome = status.State switch
        {
            GitStatusState.Succeeded => CiCheckOutcome.Passed,
            GitStatusState.Failed or GitStatusState.Error => CiCheckOutcome.Failed,
            _ => CiCheckOutcome.Pending,
        };
        var at = status.UpdatedDate != default ? status.UpdatedDate : status.CreationDate;
        return new AzureReposCiItem(new CiCheck(Name(status), outcome, false,
            new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc))), null, status.TargetUrl, status.Description);
    }
}
