using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-07-f420: what a failed run's persisted work says about itself in CommitAndPR.
/// <para>
/// The p0237 finalizer tail reaches CommitAndPR after a step has failed so the partial
/// work is not lost. The commit, the push and the draft PR are that persistence; the
/// success epilogue — the account, the keystone, the "Completed" summary, the done
/// status — is a statement about a delivered run and never applies. The ticket's own
/// word on the failure is FailureTicketComment, posted by the error path.
/// </para>
/// </summary>
public sealed class FailedRunPersistence
{
    /// <summary>The failure an earlier step left on the run; null while the run is green.</summary>
    public string? Reason(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<string>(ContextKeys.FailureReason, out var reason)
            && !string.IsNullOrWhiteSpace(reason)
            ? reason
            : null;
    }

    /// <summary>The banner a failed run's draft PR leads with, so a reviewer reads the
    /// failure before the ticket text.</summary>
    public string PullRequestBanner(string reason) =>
        $"> ⚠️ **Run failed** — {reason.Trim()}\n"
        + "> Partial work, pushed so nothing is lost. Draft for review, do not merge as-is.\n\n";

    /// <summary>The CommitAndPR step's own result on a failed run: the persistence
    /// happened or it did not; the run's failure is the failed step's to report.</summary>
    public CommandResult StepResult(string reason, IReadOnlyList<OpenedPullRequest> opened)
    {
        ArgumentNullException.ThrowIfNull(opened);
        var urls = opened.Where(o => o.Status == OpenStatus.Opened && o.Url is not null).Select(o => o.Url!).ToList();
        return urls.Count == 0
            ? CommandResult.Ok($"Run failed ({reason.Trim()}) — nothing to persist, no PR opened.")
            : CommandResult.Ok(
                $"Run failed ({reason.Trim()}) — partial work persisted as draft PR: {string.Join(", ", urls)}");
    }
}
