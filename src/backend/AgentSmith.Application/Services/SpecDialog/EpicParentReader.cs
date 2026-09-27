using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-7d9f, extracted by 2026-09-25-d0b8: reads the epic a ticket is one slice of, so
/// every child of one cut is derived against the same shared ground instead of re-inventing it
/// per ticket. It takes a provider and a ticket and nothing else, which is what lets a caller
/// that is not a pipeline ask the question.
/// <para>
/// The parent is addressed by the <c>phase-parent:</c> stamp 2026-09-13-a72a put on every filed
/// child, NOT by a reference: CreatedTicket.Reference is the web url when there is one, and
/// recovering an id from a web url is a parser per provider. Nothing new has to be produced —
/// GetTicketAsync is a non-default member of ITicketProvider that all four adapters implement.
/// </para>
/// <para>
/// 2026-09-17-0e79d: the framework files no stamped child any more, so the ground it reads is a
/// LEGACY child's or a hand-stamped ticket's. An approved epic loses nothing by it: its run
/// derives nothing — it works a set cut with the whole programme in view, in the conversation —
/// and ground for a cut that is not being made is not a loss.
/// </para>
/// <para>
/// A missing parent DEGRADES, for every caller, per <see cref="EpicParentRead.Degraded"/>. A
/// declared TEMPLATE that cannot be opened fails instead — it was declared as governing. An
/// epic that is gone was not.
/// </para>
/// </summary>
public sealed class EpicParentReader(ILogger<EpicParentReader> logger)
{
    public async Task<EpicParentRead> ReadAsync(
        ITicketProvider provider, Ticket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(ticket);

        var parentId = FiledTicketLabels.ParentId(ticket.Labels);
        if (string.IsNullOrEmpty(parentId)) return EpicParentRead.Unstamped;

        try
        {
            var parent = await provider.GetTicketAsync(new TicketId(parentId), cancellationToken);
            logger.LogInformation(
                "Epic ground read from parent ticket {ParentId} ({Title})", parentId, parent.Title);
            // 2026-09-18-d518: the parent is a SECOND ticket and never went through the fetch
            // handler's door, so the framework's own label note comes off its body here.
            return EpicParentRead.Opened(new EpicGround(
                parentId, parent.Title, TicketLabelNoteStripper.Strip(parent.Description)));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Epic parent {ParentId} could not be read — the caller proceeds on its own ticket",
                parentId);
            return EpicParentRead.Degraded(parentId, ex.Message);
        }
    }
}
