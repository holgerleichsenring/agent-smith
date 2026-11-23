using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-8d16: before a merge a person asked for is approved and completed, a pull request the host reads as a draft is
/// taken out of draft (<see cref="ISourceProvider.MarkPullRequestReadyAsync"/>) — Azure DevOps cannot complete a draft, and
/// the click is the person's word that it is ready. One the host reads as no draft is left alone (the Azure and GitLab
/// updaters write unconditionally); one whose state cannot be read is asked anyway, since a needless mark is harmless. A host
/// that keeps the draft is a refusal naming it.
/// </summary>
public sealed class PullRequestDraftStep(ISourceProviderFactory sources)
{
    public async Task<string?> RefusalAsync(RepoConnection repo, string prUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (repo.Type == RepoType.Local) return null;
        var provider = sources.Create(repo);
        try
        {
            if (!await IsDraftAsync(provider, prUrl, ct)) return null;
            return await provider.MarkPullRequestReadyAsync(prUrl, ct) ? null : Refused(null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Refused(ex.Message); }
    }

    // True when the host reads it a draft, or when its state cannot be read.
    private static async Task<bool> IsDraftAsync(ISourceProvider provider, string prUrl, CancellationToken ct)
    {
        if (provider is not IPrReviewStateReader states) return true;
        try { return (await states.ReadAsync(prUrl, ct)).IsDraft; }
        catch (Exception ex) when (ex is not OperationCanceledException) { return true; }
    }

    private static string Refused(string? reason) =>
        $"still a draft: the host did not take it out of draft{(reason is null ? string.Empty : $" ({reason})")}; left open";
}
