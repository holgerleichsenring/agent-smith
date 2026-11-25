using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-11-25-9992g: a tracker comment on a ticket a series holds is the series master's — never a rework. On an issue
/// tracker that is a repository (GitHub, GitLab), where anyone may comment on a public one, it reaches the master only when
/// its author can write to the repository, the bar a pull-request command has; a stranger's comment is dropped. Jira and
/// Azure DevOps boards are only open to their organisation's signed-in members, so their comments are taken as they come.
/// </summary>
public sealed class SeriesTicketCommentWake(
    ISeriesRunStore series, ISeriesWakes wakes, IPrReviewAuthorTrust trust, ILogger<SeriesTicketCommentWake> logger)
{
    /// <summary>True when a series of the project holds the ticket — the comment woke its master, or was dropped as a
    /// stranger's; either way the standalone path is not taken.</summary>
    public async Task<bool> TakenAsync(string project, KeywordComment comment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comment);
        if (comment.Envelope.TicketId is not { Length: > 0 } ticketId || !await series.HoldsTicketAsync(project, ticketId, ct))
            return false;
        if (!await TrustedAsync(comment, ct))
        {
            logger.LogInformation("Comment by {Author} on series ticket {Ticket}: the author may not write to the repository; dropped",
                comment.Act?.Author, ticketId);
            return true;
        }
        await wakes.WakeAsync(project, ticketId, new SeriesWake(SeriesWakeKinds.Comment, comment.Body.Trim(), comment.Act?.Author), ct);
        return true;
    }

    private async Task<bool> TrustedAsync(KeywordComment comment, CancellationToken ct) => comment.Envelope.Platform switch
    {
        "github" => comment.Writer is { } writer && await trust.IsTrustedAsync(RepoType.GitHub, writer, ct),
        "gitlab" => comment.Writer is { } writer && await trust.IsTrustedAsync(RepoType.GitLab, writer, ct),
        _ => true,
    };
}
