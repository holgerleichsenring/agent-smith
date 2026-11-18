using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Models;

/// <summary>2026-11-18-c8e2d: one follow-up a spec's master proposed, with the run and place it is filed by.</summary>
public sealed record TicketSeriesFollowUpView(string Goal, string Why, string RunId, int Index)
{
    /// <summary>The follow-ups of one spec in a stored view, numbered within that spec as its own report numbers them.</summary>
    public static IReadOnlyList<TicketSeriesFollowUpView>? Of(AcceptanceView? view, string specId, string? runId) =>
        view?.FollowUps is not { } all || runId is null ? null
            : [.. all.Where(f => f.SpecId is null || f.SpecId == specId)
                .Select((f, i) => new TicketSeriesFollowUpView(f.Goal, f.Why, runId, i))];

    /// <summary>2026-11-18-98fa: the spec's master's report in a stored view; null when the view carries none.</summary>
    public static string? ReportOf(AcceptanceView? view, string specId) =>
        view?.Reports?.FirstOrDefault(r => r.SpecId is null || r.SpecId == specId)?.Markdown;
}
