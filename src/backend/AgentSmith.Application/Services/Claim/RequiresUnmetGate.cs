using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-10-14-dbceh: refuses a code claim of a ticket a ticket it requires has not landed for, or whose
/// filing did not finish. Strict: no start by name and no series auto-start passes it — marking the
/// required ticket done by hand lifts it everywhere at once. A ticket without requirement rows costs one read.
/// 2026-11-19-1b72a: a second hold, with its own reason (ClaimRejectionReason.SeriesPaused) — a ticket of a paused series that no run worked yet waits for the
/// series' resume. A worked ticket's retry, rework or CI fix is not a new ticket and is not held, so none is lost.
/// </summary>
public sealed class RequiresUnmetGate(UnmetTicketRequirements requirements, IPreviousAttemptReader? attempts = null)
{
    public const string IncompleteFiling = "This ticket's filing did not finish; close it and approve the list again.";

    /// <summary>2026-11-19-1b72a: the hold of a paused series.</summary>
    public const string SeriesPaused =
        "This ticket's series is paused, so it does not start; resume the series in its Work it out conversation (say so, or press Resume).";

    /// <summary>The refusal, or null when nothing the ticket requires holds it.</summary>
    public async Task<ClaimResult?> RefusalAsync(ClaimRequest request, AgentSmithConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);
        if (request.PipelineName != PipelinePresets.CodeName
            || !config.Projects.TryGetValue(request.ProjectName, out var project)) return null;
        if (await PausedAsync(project.Name, request.TicketId.Value, request.ExistingRunId, cancellationToken))
            return ClaimResult.Rejected(ClaimRejectionReason.SeriesPaused, SeriesPaused);
        var tracker = project.Tracker;
        var key = TicketKey.For(tracker.Type.ToString().ToLowerInvariant(), request.TicketId.Value).Value;
        var hold = await requirements.ForStartAsync(project, key, cancellationToken); // 2026-11-17-9a39a
        if (!hold.HasRequirements) return null;
        if (!await requirements.FiledCompletelyAsync(tracker.Name, key, cancellationToken))
            return ClaimResult.Rejected(ClaimRejectionReason.TicketRequiresUnmet, IncompleteFiling);
        return hold.Unmet.Count == 0 ? null
            : ClaimResult.Rejected(ClaimRejectionReason.TicketRequiresUnmet, Sentence(hold.Unmet, hold.GreenLifts));
    }

    // The claim's own reserved run is no earlier attempt (as BoundaryStopGate reads it).
    private async Task<bool> PausedAsync(string project, string ticketId, string? ownRunId, CancellationToken ct) =>
        await requirements.PausedSeriesAsync(project, ticketId, ct) is not null
        && (attempts is null || await attempts.LatestAsync(project, ticketId, ownRunId, ct) is null);

    /// <summary>What the ticket waits for, each required ticket named "#n" when its id is numeric and as written otherwise.
    /// 2026-11-17-9a39a: under on_green_push a green pushed branch is named as the other way to start it.</summary>
    public static string Sentence(IReadOnlyList<TicketRequirementEdge> unmet, bool greenLifts = false)
    {
        ArgumentNullException.ThrowIfNull(unmet);
        var named = string.Join(", ", unmet.Select(e => Named(e.RequiredTicketId)));
        return greenLifts
            ? $"This ticket waits for {named} to push a green branch it can build on (requires_start: on_green_push), or to be merged; "
              + "merge them, or mark them done by hand."
            : $"This ticket waits for {named} to be merged first; merge them, or mark them done by hand.";
    }

    private static string Named(string id) => id.Length > 0 && id.All(char.IsAsciiDigit) ? $"#{id}" : id;
}
