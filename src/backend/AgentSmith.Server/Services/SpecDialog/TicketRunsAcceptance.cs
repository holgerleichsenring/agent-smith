using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-13-9a59o: one spec's stored acceptance snapshot in the shape ticket_runs answers with —
/// AcceptanceView's own fields mapped, nothing re-judged. A snapshot that cannot be read leaves the
/// three accounts null rather than empty: empty would claim the judge found nothing.
/// </summary>
public static class TicketRunsAcceptance
{
    public static TicketRunSpec Of(string specId, string status, string? verdict, string? acceptanceJson)
    {
        var view = Read(acceptanceJson);
        return view is null
            ? new TicketRunSpec(specId, status, verdict, null, null, null)
            : new TicketRunSpec(specId, status, verdict,
                [.. view.Criteria.Select(c => new TicketRunCriterion(c.Text, c.Status, c.Reason, c.Citation))],
                [.. (view.Declined ?? []).Select(d => new TicketRunDeclined(d.Text, d.Reason))],
                [.. view.Unmeasured.Select(u => new TicketRunUnmeasured(u.Repositories, u.Reason))]);
    }

    /// <summary>A snapshot whose JSON parses but lacks its criteria fails in AcceptanceView's own
    /// lift rather than in the parser, so that failure is an unreadable snapshot too.</summary>
    private static AcceptanceView? Read(string? json)
    {
        try
        {
            return RunStoryJson.TryDeserialize<AcceptanceView>(json);
        }
        catch (ArgumentNullException)
        {
            return null;
        }
    }
}
