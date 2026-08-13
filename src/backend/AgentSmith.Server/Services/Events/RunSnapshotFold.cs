using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0404: the LIVE fold that advances a <see cref="RunSnapshot"/> event by event,
/// split out of the record itself — the snapshot is the dashboard's contract, this
/// is how the broadcaster moves it forward. Pure function over values, so it stays
/// static and needs no DI; an extension method keeps every call site unchanged.
/// </summary>
public static class RunSnapshotFold
{
    public static RunSnapshot Apply(this RunSnapshot snapshot, RunEvent runEvent) => runEvent switch
    {
        RunStartedEvent e => snapshot with
        {
            Pipeline = e.Pipeline, Trigger = e.Trigger, Repos = e.Repos,
            Status = "running", StartedAt = e.StartedAt, LastEventType = e.Type.ToString(),
            AgentName = e.AgentName ?? snapshot.AgentName,
            // p0211: ticket id at run start feeds the title fallback label
            // before any TicketFetchedEvent (and for runs that never fetch one).
            TicketId = e.TicketId ?? snapshot.TicketId,
        },
        // p0176b: RunFinished.CostUsd, when present, overrides the per-call
        // accumulation. Defence in depth: even if a producer leaked LLM
        // calls past the factory wrap, the run-end truth lands here.
        RunFinishedEvent e => snapshot with
        {
            Status = e.Status, PrUrl = e.PrUrl, Summary = e.Summary,
            FinishedAt = e.FinishedAt,
            CostUsd = e.CostUsd ?? snapshot.CostUsd,
            LastEventType = e.Type.ToString()
        },
        SandboxCreatedEvent => snapshot with
        {
            Sandboxes = snapshot.Sandboxes + 1, LastEventType = runEvent.Type.ToString()
        },
        StepStartedEvent e => snapshot with
        {
            StepIndex = e.StepIndex, StepName = e.StepName, TotalSteps = e.TotalSteps,
            LastEventType = e.Type.ToString()
        },
        StepFinishedEvent e => snapshot with
        {
            LastEventType = e.Type.ToString()
        },
        // p0175-fix: LLM cost rolls up onto the run snapshot so the
        // /system CostRollupCard can read it from the overview without
        // a separate cross-stream subscription. Per-event granularity
        // is preserved in the run-stream; snapshot keeps the running
        // total for fast dashboard reads.
        LlmCallFinishedEvent e => snapshot with
        {
            CostUsd = snapshot.CostUsd + (decimal)e.CostUsd,
            LlmCalls = snapshot.LlmCalls + 1,
            LlmDurationMs = snapshot.LlmDurationMs + e.DurationMs,
            ThrottleWaitMs = snapshot.ThrottleWaitMs + e.ThrottleWaitMs,
            LastEventType = e.Type.ToString()
        },
        // p0357: the resolved budget lands live on the snapshot — the runs page
        // shows spent/cap without waiting for the REST refetch.
        RunBudgetResolvedEvent e => snapshot with
        {
            BudgetTier = e.Tier, BudgetCapUsd = e.CapUsd, BudgetCapTokens = e.CapTokens,
            LastEventType = e.Type.ToString()
        },
        // p0184: copy ticket id + title onto the snapshot so the runs-page
        // card has the human-readable heading at-a-glance. Description /
        // attachments stay on the event for the Fetch-ticket step body to
        // read on drill-in.
        TicketFetchedEvent e => snapshot with
        {
            TicketId = e.TicketId,
            TicketTitle = e.Title,
            LastEventType = e.Type.ToString()
        },
        // p0200: cancel-requested flips the snapshot bit; the terminal
        // RunFinished still drives the move from Active to Recent.
        RunCancelRequestedEvent e => snapshot with
        {
            CancelRequested = true,
            CancelReason = e.Reason,
            LastEventType = e.Type.ToString()
        },
        // p0350: an opened PR now lands on the LIVE snapshot too (was trail-only,
        // so the live card showed no PR until the REST refetch). Accumulate per
        // repo and seed the primary PrUrl. Draft-ness is only known at run end, so
        // the live view marks non-draft; the REST refetch (RunSnapshotMapper)
        // carries the authoritative flag.
        PullRequestOutcomeEvent e when e.Status == "opened" && !string.IsNullOrEmpty(e.Url) => snapshot with
        {
            PrUrl = snapshot.PrUrl ?? e.Url,
            PullRequests = AppendPr(
                snapshot.PullRequests, new RunPullRequestView(e.Repo, e.Url!, e.Status, IsDraft: false)),
            LastEventType = e.Type.ToString()
        },
        _ => snapshot with { LastEventType = runEvent.Type.ToString() }
    };

    // p0350: upsert a PR by repo (a repeat outcome for the same repo replaces,
    // never duplicates) so the live list mirrors the per-repo DB rows.
    private static IReadOnlyList<RunPullRequestView> AppendPr(
        IReadOnlyList<RunPullRequestView>? existing, RunPullRequestView pr)
    {
        var list = existing is null
            ? new List<RunPullRequestView>()
            : existing.Where(p => p.Repo != pr.Repo).ToList();
        list.Add(pr);
        return list;
    }
}
