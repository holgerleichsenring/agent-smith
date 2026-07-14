using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0326: a ticket carried inline on the <see cref="PipelineRequest"/> instead
/// of living in a tracker. FetchTicket materializes it directly and skips the
/// provider lookup, so a run without any configured tracker (the demo) still
/// exercises the real fix-bug preset — not a special demo pipeline.
/// </summary>
public sealed record InlineTicket(
    string Title,
    string Description,
    string? ReproSteps = null)
{
    public const string Source = "inline";

    /// <summary>The materialized requirement record, repro appended to the body.</summary>
    public Ticket ToTicket() => new(
        new TicketId(Source),
        Title,
        ReproSteps is null ? Description : $"{Description}\n\nReproduction:\n{ReproSteps}",
        acceptanceCriteria: null,
        status: "open",
        source: Source);
}
