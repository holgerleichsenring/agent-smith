using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: one ticket of a filing plan. A ticket with a <see cref="Series"/> is stamped, recorded with that
/// series and written to its branch; one without (a bug) is only created.
/// </summary>
/// <param name="Key">The plan's own name for it — a list ticket's key, an epic's part — which requires refer to.</param>
/// <param name="Role">The filing role an installation maps to a native work-item type.</param>
/// <param name="Render">The title and body, given each required ticket's tracker key and, for a sibling, its title.</param>
/// <param name="Series">The approved specs it carries; null for a ticket with no record.</param>
/// <param name="Goal">The series' goal, and what a note names the ticket by.</param>
/// <param name="Requires">Earlier plan keys, or <c>ticket:&lt;id&gt;</c> of a ticket already filed.</param>
/// <param name="Labels">What it is created with: the approved-set stamp only with a series.</param>
public sealed record PlannedTicket(
    string Key, TicketFilingRole Role, Func<IReadOnlyList<(string Key, string? Title)>, SpecTicketContent> Render,
    FiledSeries? Series, string Goal, IReadOnlyList<string> Requires, IReadOnlyList<string> Labels)
{
    /// <summary>The repositories its record names; empty means the approval's scope.</summary>
    public IReadOnlyList<string> Repos { get; init; } = [];
}
