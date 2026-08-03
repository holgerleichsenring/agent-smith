using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: posts the derived cut back to the ticket as "this is how I understood it",
/// and the run PROCEEDS.
/// <para>
/// Ratification is non-blocking on purpose: blocking would reintroduce exactly the wait
/// that Approval was deleted for, and a correcting comment is the re-run path anyway. The
/// risk this accepts is that a wrong reading gets built before anyone objects; it is
/// bounded by the pull request and by the discarded list being visible in both places.
/// </para>
/// <para>
/// The comment is an OUTPUT, never an input: the next run reads the branch artifact, not
/// this comment, or it would be feeding on its own echo.
/// </para>
/// </summary>
public sealed class SpecSetTicketCommenter(
    ITicketProviderFactory ticketFactory,
    ILogger<SpecSetTicketCommenter> logger)
{
    /// <summary>Marks the comment as this system's reading, so a human can tell it from an answer.</summary>
    public const string Marker = "<!-- agentsmith:derived-spec -->";

    public async Task PostAsync(
        PipelineContext pipeline, TrackerConnection? tracker, SpecSet set, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (tracker is null || set.Phases.Count == 0) return;
        if (!pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null) return;
        // An inline ticket exists only on this run — there is nothing to comment on.
        if (pipeline.Has(ContextKeys.InlineTicket)) return;

        try
        {
            await ticketFactory.Create(tracker).UpdateStatusAsync(ticket.Id, Build(set, pipeline), ct);
            logger.LogInformation(
                "Posted the derived cut of {Key} ({Phases} phase(s)) to ticket {Ticket}",
                set.Key, set.Phases.Count, ticket.Id.Value);
        }
        catch (Exception ex)
        {
            // The artifacts are on the branch and in the pull request either way; a tracker
            // that refuses a comment must not end a run that is otherwise fine.
            logger.LogWarning(ex, "Could not post the derived cut to ticket {Ticket}", ticket.Id.Value);
        }
    }

    private static string Build(SpecSet set, PipelineContext pipeline)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        sb.AppendLine("## Agent Smith — this is how I understood the ticket");
        sb.AppendLine();
        sb.AppendLine(
            "I split it into the phases below and started working. This is NOT a question and "
            + "the run is not waiting: comment if the cut is wrong and the next run amends an "
            + "unstarted phase or re-cuts the unstarted tail. A phase that already ran is never "
            + "edited — a correction to it becomes a new phase.");
        sb.AppendLine();
        sb.Append(SpecPrBody.RenderPhases(set));
        sb.AppendLine();
        sb.Append(SpecPrBody.RenderDiscarded(set));
        if (pipeline.TryGet<string>(ContextKeys.SpecPullRequestUrl, out var url)
            && !string.IsNullOrWhiteSpace(url))
        {
            sb.AppendLine();
            sb.AppendLine($"The specs are open for review: {url}");
        }
        return sb.ToString();
    }
}
