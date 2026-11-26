using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-11-25-9992g: a comment on the pull request of a ticket a series holds goes to the series master — a command as much
/// as plain text — instead of a rework. Its author must be able to write to the repository, the bar a command has, so a
/// stranger's text on a public pull request never reaches the master that merges.
/// </summary>
public sealed class SeriesPrCommentWake(IPrTicketOwner owners, ISeriesRunStore series, ISeriesWakes wakes, ILogger<SeriesPrCommentWake> logger)
{
    /// <summary>True when the comment woke the series holding the ticket; false leaves it to the standalone path.</summary>
    public async Task<bool> TryAsync(PrCommentCommand command, string ticketId, IPrCommentAuthorTrust trust,
        IReadOnlySet<string>? allowedProjects, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(trust);
        if (await owners.FindAsync(command.Author.RepositoryUrl, ticketId, allowedProjects, ct) is not { } owner
            || !await series.LeadsCommentsAsync(owner.ProjectName, ticketId, ct) // 96aea
            || !await trust.IsTrustedAsync(command.Author, ct)) return false;
        logger.LogInformation("PR comment from {Author} on {Pr}: woke the series holding ticket {Ticket}",
            command.Author.AuthorLogin, command.PrLabel, ticketId);
        return await wakes.WakeAsync(owner.ProjectName, ticketId,
            new SeriesWake(SeriesWakeKinds.Comment, command.Body.Trim(), command.Author.AuthorLogin) { PrUrl = command.PrUrl }, ct);
    }
}
