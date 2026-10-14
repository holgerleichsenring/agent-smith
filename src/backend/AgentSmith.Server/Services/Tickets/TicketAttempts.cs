using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: which runs of a ticket are attempts at its work — the code-pipeline runs,
/// ordered by run id, a live capacity reservation included (it keeps its id when promoted).
/// <para>
/// A CANCELLED code run that never started a step did nothing — a reservation cancelled from the
/// queue, or a request the queued-run sweeper flagged interrupted before RunStarted — and is not
/// an attempt. A FAILED one is a launch whose prologue threw, which a person must see, so it is.
/// This is deliberately not the previous-attempt reader's rule, which drops every queued row.
/// </para>
/// </summary>
public sealed class TicketAttempts
{
    /// <summary>The status every cancel path finalizes a run with.</summary>
    public const string Cancelled = "cancelled";

    public bool IsAttempt(TicketRunRow row) =>
        row.Run.Pipeline == PipelinePresets.CodeName && !(row.Run.Status == Cancelled && !row.HasStep);

    /// <summary>The attempts among the rows, oldest first.</summary>
    public IReadOnlyList<TicketRunRow> Of(IEnumerable<TicketRunRow> rows) =>
        [.. rows.Where(IsAttempt).OrderBy(r => r.Run.Id, StringComparer.Ordinal)];

    /// <summary>The newest attempt, or the newest row when none of them is an attempt.</summary>
    public TicketRunRow Head(IReadOnlyList<TicketRunRow> rows) =>
        rows.Where(IsAttempt).MaxBy(r => r.Run.Id, StringComparer.Ordinal)
        ?? rows.MaxBy(r => r.Run.Id, StringComparer.Ordinal)
        ?? throw new ArgumentException("A ticket has at least one run.", nameof(rows));
}
