using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-09-25-8e51e: replacing the framework's own region of a ticket body — the one write that
/// needs a READ-MODIFY-WRITE of a field a person also writes in.
/// <para>
/// A CAPABILITY PORT OF ITS OWN rather than a member of <see cref="ITicketProvider"/>. The
/// spec asked for a partial of the port's file; the code refuses it: every one of the four
/// provider classes is already over the file-length baseline and may only get shorter, so a
/// member every provider must implement could only land by making those four classes partial —
/// splitting a type across files to get under a per-file limit, which is the one thing that
/// limit exists to prevent. Four small implementations beside them cost nothing and each is
/// readable on its own.
/// </para>
/// <para>
/// JIRA IMPLEMENTS IT TOO, AND REFUSES. A tracker that simply had no implementation would be
/// reported as "no rewriter for this tracker", which is a fact about our wiring; what an
/// operator needs is the fact about JIRA — a description is stored as a structured document and
/// read back as text nodes only, so a round trip drops mentions, emoji, media, inline cards and
/// status nodes and flattens every mark, heading level, list marker and table. That sentence can
/// only live where the refusal is.
/// </para>
/// </summary>
public interface ITicketRewriter
{
    /// <summary>
    /// Replaces the region <see cref="FramedTicketRegion"/> marks in the ticket's body with
    /// <paramref name="region"/>, leaving every character outside the markers as it stands.
    /// Never throws — see <see cref="TicketRewriteResult"/>.
    /// <para>
    /// A body with no complete marker pair is <see cref="TicketRewriteOutcome.Unsupported"/>:
    /// it was filed before the framework marked its region, and nothing in it says where a
    /// person's prose begins.
    /// </para>
    /// </summary>
    /// <param name="region">A whole rendering including both markers, as
    /// <see cref="FramedTicketRegion.Wrap"/> produces one.</param>
    Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, CancellationToken cancellationToken);
}
