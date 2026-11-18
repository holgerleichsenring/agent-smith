using System.Net;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-07-f420: the comment a FAILED run leaves on its ticket. HTML-formatted: AzDO
/// System.History accepts HTML; GitHub/GitLab markdown comments render inline HTML; only
/// Jira's ADF flattens it to plain text (an acceptable fallback).
/// <para>
/// It is the ticket's one comment on a failed run, so a step failure also says what the
/// finalizer tail kept and where — the draft PR CommitAndPR published on the context and
/// the branch — instead of a second comment a reader has to reconcile with the first.
/// </para>
/// </summary>
public sealed class FailureTicketComment
{
    private const string Heading = "<b>Agent Smith — Failed</b><br/>";

    public string ForStep(CommandResult failure, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(pipeline);
        return Heading
            + $"<b>Step:</b> {WebUtility.HtmlEncode(failure.StepName)} ({failure.FailedStep}/{failure.TotalSteps})<br/>"
            + $"<b>Error:</b> {WebUtility.HtmlEncode(failure.Message ?? string.Empty)}"
            + PersistedWork(pipeline);
    }

    public string ForFatal(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var message = string.IsNullOrEmpty(exception.Message) ? exception.GetType().Name : exception.Message;
        return Heading + $"<b>Error:</b> {WebUtility.HtmlEncode(message)}";
    }

    public string ForMessage(string message) => Heading + WebUtility.HtmlEncode(message);

    /// <summary>What was kept and where; empty when nothing was persisted.</summary>
    public string PersistedWork(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, out var opened)
            || opened is null)
            return string.Empty;
        var urls = opened.Where(o => o.Status == OpenStatus.Opened && o.Url is not null).Select(o => o.Url!).ToList();
        if (urls.Count == 0) return string.Empty;
        var branch = pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) && repository is not null
            ? $" on branch <code>{WebUtility.HtmlEncode(repository.CurrentBranch.Value)}</code>"
            : string.Empty;
        return $"<br/><b>Partial work:</b> kept as draft PR {string.Join(", ", urls)}{branch} — not merged, not completed.";
    }
}
