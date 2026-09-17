using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79a: a missing set is LOUD. A ticket the framework filed from an approved set
/// carries <see cref="FiledTicketLabels.ApprovedSetStamp"/> and no fenced spec, so if it reaches
/// DeriveSpec with no branch artifact, no carried record and no record in a store this process
/// has, the hand-off broke. Deriving then would silently replace a ratified spec with a guess.
/// <para>
/// The gate keys on the STAMP and not on the bare phase label, and the source precedence
/// consults it BEFORE the ticket description. A hand-written phase ticket carries the label too
/// and its spec legitimately lives in its description; a FILED one carries the stamp, and a
/// fenced block pasted into its description by anyone with tracker access is exactly the second
/// editable truth this phase removed — so on a stamped ticket the description is not consulted
/// at all.
/// </para>
/// <para>
/// The EXEMPTION is carried here rather than left to 2026-09-17-0e79d, because the rule and the
/// tickets it would break must land together: a ticket carrying a <c>phase-parent:</c> stamp is
/// an epic child of the N-children shape, which deliberately carries no spec, and still derives.
/// Every other ticket derives as before.
/// </para>
/// </summary>
public sealed class FiledTicketSpecGate(ILogger<FiledTicketSpecGate> logger)
{
    /// <summary>What is missing, or null when this ticket may derive its own spec.</summary>
    public string? MissingSet(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var labels = ticket.Labels ?? [];
        if (!FiledTicketLabels.CarriesApprovedSet(labels)) return null;
        if (FiledTicketLabels.ParentId(labels) is { } parent)
        {
            logger.LogInformation(
                "Ticket {Ticket} is an epic child of {Parent} and carries no spec by design — deriving one",
                ticket.Id.Value, parent);
            return null;
        }
        return $"ticket {ticket.Id.Value} carries '{FiledTicketLabels.ApprovedSetStamp}', so it was "
            + "filed from an approved specification — but this run found no set on the ticket "
            + "branch, no approved record on its initial context and none in a store this process "
            + "can read. The approval never reached the run; deriving one here, or reading a spec "
            + "out of a description anyone can edit, would replace a ratified specification with a "
            + "guess";
    }
}
