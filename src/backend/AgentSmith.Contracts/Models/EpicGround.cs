namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-13-7d9f: the epic a run's ticket is one slice of, as the run reads it — the
/// parent's own requirement body, fetched once together with the ticket.
/// <para>
/// It is the WHAT that binds every slice of one cut: its vocabulary, the contracts between
/// the slices, and the decisions a workshop already settled. A template gives the FORM —
/// how a handler is written here — and it cannot give this. In a brownfield repository the
/// existing code carries it; in a GREENFIELD one nothing does, and each child of an epic
/// invents its own. The parent already holds what a person confirmed; the defect this
/// closes is that no run could see it.
/// </para>
/// </summary>
/// <param name="ParentTicketId">
/// The parent's ticket id, read off the child's own <c>phase-parent:</c> stamp
/// (2026-09-13-a72a). A CreatedTicket.Reference is the web url when there is one, and
/// recovering an id from a web url is a parser per provider.
/// </param>
/// <param name="Title">The parent's title, so the section can name what it is showing.</param>
/// <param name="Body">
/// The parent's description. 2026-09-13-b7ba made it a REQUIREMENT body — goal, reasoning,
/// scope in and out, requires, the ordered slice list — and no fenced yaml block, which is
/// what makes it safe to hand to five children at once.
/// </param>
public sealed record EpicGround(string ParentTicketId, string Title, string Body);
