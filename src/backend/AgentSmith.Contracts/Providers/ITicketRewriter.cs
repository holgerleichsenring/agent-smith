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
/// 2026-10-28-7a69b: JIRA IMPLEMENTS IT ON THE DOCUMENT. It used to refuse, because reading a
/// description as text drops mentions, links, marks and tables; it now replaces the region's
/// paragraphs inside the stored document and sends every other node back as it was read.
/// </para>
/// </summary>
public interface ITicketRewriter
{
    /// <summary>
    /// Replaces the region <see cref="FramedTicketRegion"/> marks in the ticket's body with
    /// <paramref name="region"/>, leaving every character outside the markers as it stands.
    /// Never throws — see <see cref="TicketRewriteResult"/>.
    /// <para>
    /// A body with no complete marker pair is <see cref="TicketRewriteOutcome.Unsupported"/> under
    /// <see cref="RegionPlacement.ReplaceOnly"/>: the framework rendered it whole before it marked its
    /// region, and nothing says where a person's prose begins. Under
    /// <see cref="RegionPlacement.ReplaceOrAppend"/> it is a person's text and the region goes below it.
    /// </para>
    /// </summary>
    /// <param name="region">A whole rendering including both markers, as
    /// <see cref="FramedTicketRegion.Wrap"/> produces one.</param>
    /// <param name="placement">2026-10-28-7a69b: whether an unmarked body takes the region below it.</param>
    Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, RegionPlacement placement, CancellationToken cancellationToken);
}
