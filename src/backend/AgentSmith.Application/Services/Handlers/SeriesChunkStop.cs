using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-8762u: the commit-and-PR end of an attempt that stopped at a series boundary — an ordinary end, not
/// a failure and not a delivery. The pull request stays a draft and says how far the series is; the ticket gets
/// ONE comment naming what was built, what is left and where the next attempt continues. It never finalizes: the
/// done status would report a series done after its first chunk, so the ticket keeps its status and approval.
/// </summary>
public sealed class SeriesChunkStop(ITicketProviderFactory tickets, ILogger<SeriesChunkStop> logger)
{
    public string PullRequestBanner(SpecSequenceProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return $"> 🧩 **{Sentence(progress)}** Draft until the series is through.\n\n";
    }

    /// <summary>Posts the boundary comment (none for an inline ticket, which exists only on its run) and answers
    /// the step's own result with the same sentence.</summary>
    public async Task<CommandResult> ReportAsync(CommitAndPRContext context, SpecSequenceProgress progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(progress);
        var sentence = Sentence(progress);
        if (context.Pipeline.Has(ContextKeys.InlineTicket)) return CommandResult.Ok(sentence);
        try
        {
            await tickets.Create(context.TrackerConnection).UpdateStatusAsync(context.Ticket.Id, Comment(progress), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The draft pull request carries the same standing; a tracker refusing a comment ends nothing.
            logger.LogWarning(ex, "Could not post the series boundary to ticket {Ticket}", context.Ticket.Id.Value);
        }
        return CommandResult.Ok(sentence);
    }

    private static string Sentence(SpecSequenceProgress progress)
    {
        var built = progress.Specs.Count(p => p.State == SpecRunState.Done);
        var next = progress.Specs.First(p => p.NextAttempt).SpecId;
        return $"Series in progress: {built} of {progress.Specs.Count} specs built; the next attempt continues with {next}.";
    }

    private static string Comment(SpecSequenceProgress progress) =>
        $"Agent Smith — {Sentence(progress)}\n\n"
        + $"Built: {string.Join(", ", progress.Specs.Where(p => p.State == SpecRunState.Done).Select(p => p.SpecId))}\n"
        + $"Left: {string.Join(", ", progress.Specs.Where(p => p.NextAttempt).Select(p => p.SpecId))}\n\n"
        + "To change the remaining specs, comment on this ticket — a \"Request changes\" review on the draft pull "
        + "request re-cuts the series' tail.";
}
