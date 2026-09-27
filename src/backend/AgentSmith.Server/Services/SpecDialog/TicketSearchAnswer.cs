namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>One ticket a person may pick, the tracker it is on, and the projects routed to it.</summary>
/// <param name="Exact">2026-09-27-481bb: this is the ticket whose NUMBER was typed, not one whose
/// text mentions it. Already first in the answer — it is added before the text sweep and the cap
/// keeps it — so what this adds is the ability to SAY so.</param>
public sealed record TicketSearchFound(
    string TicketId, string Title, string Tracker, IReadOnlyList<string> Projects,
    bool Exact = false);

/// <summary>
/// What the sweep found, whether the cap bit, and which trackers could not be searched — the last
/// of which is the difference between an empty board and an unanswered question.
/// </summary>
/// <param name="Unreachable">2026-09-27-481bb: trackers the NUMBER lookup could not ask. Apart
/// from <paramref name="Unsearchable"/> on purpose: a tracker whose text search worked and whose
/// number read failed is not one nothing is known about, and one sentence cannot say both.</param>
public sealed record TicketSearchAnswer(
    IReadOnlyList<TicketSearchFound> Found, bool MoreHeldBack, IReadOnlyList<string> Unsearchable,
    IReadOnlyList<string> Unreachable);
