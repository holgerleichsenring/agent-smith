using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.Events;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace AgentSmith.Server.Hubs;

/// <summary>
/// Dashboard fan-out hub. Three Subscribe methods route clients into the
/// overview / per-run / per-sandbox SignalR groups. On SubscribeOverview /
/// SubscribeRun we replay the retained stream window before live tail
/// starts; the MAXLEN=10000 bound is part of the contract — clients see
/// the oldest retained event as the start of their visible history.
/// </summary>
public sealed class JobsHub(
    JobsBroadcaster broadcaster,
    SandboxExpansionRegistry expansionRegistry,
    IConnectionMultiplexer redis,
    TrailReader trailReader,
    ResultMarkdownReader resultReader,
    PlanMarkdownReader planReader,
    AnalyzeMarkdownReader analyzeReader) : Hub
{
    // p0246f: the run list + detail are served from the DB system-of-record over
    // REST (GET /api/runs, RunQueryEndpoints) — survives a process restart AND a
    // Redis flush. This hub now carries only the live transport: the per-run
    // event stream, the system feed, sandbox expansion, and the RunsChanged nudge
    // that tells the dashboard to refetch. result.md/plan.md still come from the
    // DB-backed artifact store (p0246e) via the markdown readers below.
    public async Task SubscribeOverview()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Overview);
        // p0246f: the run list/detail now come from GET /api/runs (the DB
        // system-of-record); this group carries only the live KPI rollup + the
        // RunsChanged nudge. On join we push the current SystemActivity so the
        // /system card has server-truth on first paint without a separate round
        // trip; subsequent KPI updates arrive via SystemActivityUpdated.
        await Clients.Caller.SendAsync("SystemActivityUpdated", broadcaster.GetSystemActivity());
    }

    /// <summary>
    /// p0173a: subscribes the caller to the system-level event group +
    /// replays the retained system stream window before live tail starts.
    /// The replay is XRANGE-based (full retained window, bounded by the
    /// stream's MAXLEN), matching SubscribeRun's mid-connect contract so
    /// clients see the oldest retained event as the start of their visible
    /// history. Slice a ships the pipe — producers wire up in b + c.
    /// </summary>
    public async Task SubscribeSystem()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.System);
        var db = redis.GetDatabase();
        var entries = await db.StreamRangeAsync(SystemEventStreamKeys.Stream, "-", "+");
        foreach (var entry in entries)
        {
            foreach (var pair in entry.Values)
            {
                var payload = pair.Value.ToString();
                if (string.IsNullOrEmpty(payload)) continue;
                var systemEvent = EventEnvelopeSerializer.DeserializeSystem(payload);
                if (systemEvent is null) continue;
                await Clients.Caller.SendAsync("SystemEvent", systemEvent);
            }
        }
    }

    public async Task SubscribeRun(string runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Run(runId));
        var db = redis.GetDatabase();
        var entries = await db.StreamRangeAsync(EventStreamKeys.RunStream(runId), "-", "+");
        foreach (var entry in entries)
        {
            foreach (var pair in entry.Values)
            {
                var payload = pair.Value.ToString();
                if (string.IsNullOrEmpty(payload)) continue;
                var runEvent = EventEnvelopeSerializer.Deserialize(payload);
                if (runEvent is null) continue;
                await Clients.Caller.SendAsync("RunEvent", runEvent);
            }
        }
    }

    public async Task ExpandSandbox(string runId, string repo)
    {
        expansionRegistry.Expand(runId, repo);
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Sandbox(runId, repo));
    }

    public async Task CollapseSandbox(string runId, string repo)
    {
        expansionRegistry.Collapse(runId, repo);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.Sandbox(runId, repo));
    }

    /// <summary>
    /// p0169h: returns the FULL retained event window for a run. Bounded by
    /// MAXLEN=10000 in the publisher — clients see the oldest retained event
    /// as the start of their visible history (same contract as SubscribeRun).
    /// </summary>
    public Task<IReadOnlyList<object>> GetTrail(string runId) =>
        trailReader.ReadAllAsync(runId);

    /// <summary>
    /// p0169h: paginated trail retrieval. <paramref name="fromId"/> is the
    /// stream entry id returned in the previous page's NextCursor; pass
    /// <c>"-"</c> (or null) for the first page. <paramref name="count"/>
    /// clamps to [1, 2000].
    /// </summary>
    public Task<TrailPage> GetTrailPage(string runId, string? fromId, int? count) =>
        trailReader.ReadPageAsync(runId, fromId, count);

    /// <summary>
    /// p0169j-c: returns the rendered result.md for a run from the artifact
    /// store cache (24h TTL). Returns null for unknown runs, mid-run runs
    /// before WriteRunResult, or runs whose cache has expired. Dashboard
    /// falls back to the PR URL when null.
    /// </summary>
    public Task<string?> GetResultMarkdown(string runId) =>
        resultReader.ReadAsync(runId, Context.ConnectionAborted);

    /// <summary>
    /// p0235: returns the run's plan.md from the artifact-store cache (24h TTL).
    /// For coding presets this is the agent's own plan; null when the run is
    /// unknown, the cache has expired, or no plan was written — the dashboard
    /// then hides the plan panel.
    /// </summary>
    public Task<string?> GetPlanMarkdown(string runId) =>
        planReader.ReadAsync(runId, Context.ConnectionAborted);

    /// <summary>
    /// p0243: returns the run's analyze.md from the artifact-store cache (24h TTL)
    /// — the analyzer's ProjectMap rendered as markdown, so the dashboard can show
    /// what the Analyze step understood. Null when the run is unknown, the cache
    /// has expired, or no analysis was cached; the dashboard hides the panel.
    /// </summary>
    public Task<string?> GetAnalyzeMarkdown(string runId) =>
        analyzeReader.ReadAsync(runId, Context.ConnectionAborted);
}
