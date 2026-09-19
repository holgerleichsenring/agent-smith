using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-7d9f: reads the epic a ticket is one slice of, so every child of one cut is
/// derived against the same shared ground instead of re-inventing it per ticket.
/// <para>
/// The parent is addressed by the <c>phase-parent:</c> stamp 2026-09-13-a72a put on every
/// filed child, NOT by a reference: CreatedTicket.Reference is the web url when there is
/// one, and recovering an id from a web url is a parser per provider. Nothing new has to be
/// produced — GetTicketAsync is a non-default member of ITicketProvider that all four
/// adapters implement — so the whole mechanism is a fetch and a prompt section.
/// </para>
/// <para>
/// 2026-09-17-0e79d: the framework files no stamped child any more, so the ground it reads is
/// a LEGACY child's or a hand-stamped ticket's. An approved epic loses nothing by it: its run
/// derives nothing — it works a set cut with the whole programme in view, in the conversation —
/// and ground for a cut that is not being made is not a loss.
/// </para>
/// <para>
/// A missing parent DEGRADES. It may be deleted, moved, or invisible to this token, and the
/// child's own ticket is still a complete requirement, so the failure is named on the run and
/// the run proceeds. A declared TEMPLATE that cannot be opened fails instead — it was
/// declared as governing. An epic that is gone was not.
/// </para>
/// </summary>
public sealed class EpicGroundFetcher(ILogger<EpicGroundFetcher> logger)
{
    /// <summary>
    /// Puts the parent's body on the pipeline as <see cref="ContextKeys.EpicGround"/> and
    /// returns what the fetch step should SAY about it — the empty string for the ordinary
    /// ticket that is nobody's slice, so a run outside an epic reads exactly as it did.
    /// </summary>
    public async Task<string> AttachAsync(
        ITicketProvider provider, Ticket ticket, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(pipeline);

        var parentId = FiledTicketLabels.ParentId(ticket.Labels);
        if (string.IsNullOrEmpty(parentId)) return string.Empty;

        try
        {
            var parent = await provider.GetTicketAsync(new TicketId(parentId), cancellationToken);
            // 2026-09-18-d518: the parent is a SECOND ticket, published here rather than at
            // the fetch handler's door, so the note comes off its body here.
            pipeline.Set(ContextKeys.EpicGround, new EpicGround(
                parentId, parent.Title, TicketLabelNoteStripper.Strip(parent.Description)));
            logger.LogInformation(
                "Epic ground read from parent ticket {ParentId} ({Title})", parentId, parent.Title);
            return $" — epic ground read from parent {parentId}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Epic parent {ParentId} could not be read — this run proceeds on its own ticket",
                parentId);
            return $" — epic parent {parentId} could not be read ({ex.Message}); "
                + "this run proceeds on its own ticket";
        }
    }
}
