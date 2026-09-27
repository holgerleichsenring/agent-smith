using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-5c1eb: the tickets a person can pick from after typing a few characters — every
/// configured tracker asked, bounded, and saying which of them could not answer.
/// <para>
/// THE NUMBER LOOKUP RUNS BESIDE THE TEXT SEARCH, and that is not free: one round trip per
/// configured tracker, against APIs this series records as rate limited. It is chosen anyway,
/// because a Jira key or a work-item number appears in no title or body — and the alternative, a
/// rule guessing from the text's shape which trackers could own it, would have to know four id
/// grammars, a wrong guess reading as "no such ticket" for a ticket that exists. The page's
/// debounce is what bounds the cost.
/// </para>
/// <para>
/// EVERY HIT CARRIES THE PROJECTS ROUTED TO ITS OWN TRACKER, because the wire that opens a
/// conversation carries a project and a ticket id and no tracker: the binding is re-fetched by id
/// on the project's tracker, so a hit picked from one tracker and sent with a project routed to
/// another would bind a different board's ticket of the same number, silently. The projects a
/// ticket's LABELS name are narrower and are kept where they exist, intersected with the tracker's.
/// </para>
/// </summary>
public sealed class TicketSearchAcrossTrackers(
    ITicketProviderFactory providers,
    TicketProjectChoice choice,
    ILogger<TicketSearchAcrossTrackers> logger)
{
    /// <summary>Below this nothing is asked: two characters match most of a board.</summary>
    internal const int MinimumText = 3;

    /// <summary>What one answer may hold — a screenful, over every tracker together.</summary>
    internal const int Cap = 20;

    public async Task<TicketSearchAnswer> ForAsync(
        AgentSmithConfig config, string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var typed = (text ?? string.Empty).Trim();
        var found = new List<TicketSearchFound>();
        var unsearchable = new List<string>();
        var more = false;

        var byId = await ById(config, typed, ct);
        if (byId.Found is { } exact) found.Add(exact);

        foreach (var tracker in config.Trackers.Values)
        {
            var result = await SearchAsync(tracker, typed, ct);
            if (!result.Searched)
            {
                unsearchable.Add(tracker.Name);
                continue;
            }

            more |= result.MoreHeldBack;
            var routed = TrackerProjects.RoutedTo(config, tracker.Name);
            foreach (var hit in result.Hits)
            {
                if (found.Any(held => held.Tracker == tracker.Name && held.TicketId == hit.Id.Value))
                    continue;
                found.Add(new TicketSearchFound(hit.Id.Value, hit.Title, tracker.Name, routed));
            }
        }

        return new TicketSearchAnswer(
            [.. found.Take(Cap)], more || found.Count > Cap, unsearchable, byId.Unreachable);
    }

    // The by-id sweep answers for ONE tracker — it returns on the first that has the number — so a
    // number on two boards resolves by configuration order; the text search is what shows both.
    private async Task<(TicketSearchFound? Found, IReadOnlyList<string> Unreachable)> ById(
        AgentSmithConfig config, string typed, CancellationToken ct)
    {
        var lookup = await choice.LookupAsync(config, typed, ct);
        if (lookup.Answer is not { } answer) return (null, lookup.Unreachable);
        // 2026-09-27-1bd9: the choice narrows to the answering tracker itself now, so there is
        // nothing left to intersect here — only the fall-back when its labels named nothing.
        var routed = TrackerProjects.RoutedTo(config, answer.Binding.Tracker);
        return (new TicketSearchFound(
            answer.Binding.TicketId, answer.Binding.Title, answer.Binding.Tracker,
            answer.Projects.Count > 0 ? answer.Projects : routed, Exact: true), lookup.Unreachable);
    }

    private async Task<TicketSearchResult> SearchAsync(
        TrackerConnection tracker, string typed, CancellationToken ct)
    {
        try
        {
            return await providers.CreateSearch(tracker).SearchAsync(typed, Cap, ct);
        }
        // Building it can fail too — an unconfigured secret, an unknown type. Still "could not
        // answer", never "no such ticket".
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Tracker {Tracker} could not be searched", tracker.Name);
            return TicketSearchResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }
}
