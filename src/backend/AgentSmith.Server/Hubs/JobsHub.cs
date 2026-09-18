using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Hubs;

/// <summary>
/// Dashboard fan-out hub. The Subscribe methods route clients into the overview /
/// per-run / per-sandbox / per-spec-dialog SignalR groups. On SubscribeOverview /
/// SubscribeRun we replay the retained stream window before live tail
/// starts; the MAXLEN=10000 bound is part of the contract — clients see
/// the oldest retained event as the start of their visible history.
/// </summary>
public sealed class JobsHub(
    JobsBroadcaster broadcaster,
    SandboxExpansionRegistry expansionRegistry,
    TrailReader trailReader,
    ResultMarkdownReader resultReader,
    PlanMarkdownReader planReader,
    SpecMarkdownReader specReader, // p0390
    AnalyzeMarkdownReader analyzeReader,
    SystemBacklogReader systemBacklog,
    SpecDialogOwnership dialogOwnership, FiledWorkWatch filedWork) : Hub
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
        await Clients.Caller.SendAsync("SystemActivityUpdated", broadcaster.GetSystemActivity());
    }

    /// <summary>
    /// p0173a / p0248: subscribes the caller to the system-level event group and seeds the
    /// recent tail in a SINGLE "SystemBacklog" batch before the live tail starts. Sending
    /// the backfill as one array (not one "SystemEvent" message per entry) means the
    /// dashboard renders it in one paint instead of visibly stepping through the events one
    /// by one.
    /// </summary>
    public async Task SubscribeSystem()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.System);
        await Clients.Caller.SendAsync("SystemBacklog", await systemBacklog.ReadAsync());
    }

    /// <summary>
    /// 2026-09-15-9033: joins the caller to the group their spec dialog is delivered
    /// through. A dialog id is minted by the browser and is therefore no boundary at all —
    /// on Slack the channel supplied one — so the session's OWNER is checked here. The
    /// method's permission says a caller may hold spec dialogs; it does not say which one,
    /// and this is the surface that files real tickets.
    /// </summary>
    public async Task SubscribeSpecDialog(string dialogId)
    {
        var owner = dialogOwnership.OwnerOf(Context.User);
        if (!await dialogOwnership.MayWatchAsync(dialogId, owner, Context.ConnectionAborted))
            throw new HubException($"Spec dialog '{dialogId}' belongs to another principal.");
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.SpecDialog(dialogId));
    }

    public Task WatchFiledWork(string dialogId) => filedWork.WatchAsync(Context, dialogId);

    public async Task SubscribeRun(string runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Run(runId));
        // p0291: replay the structural rail from the Redis stream MINUS SandboxOutput
        // (the stdout flood the broadcaster routes to the sandbox group, never the
        // rail). Redis is per-event, so this is real-time and complete even mid-run —
        // p0288's DB-only replay was batched and dropped just-emitted steps from a
        // live run's rail. Falls back to the durable DB trail when Redis is gone.
        foreach (var runEvent in await trailReader.ReadStructuralTrailAsync(runId))
            await Clients.Caller.SendAsync("RunEvent", runEvent);
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
    /// p0390: returns the run's work spec — the current revision plus its revision
    /// list — from the artifact-store cache. The content of record is spec.yaml on
    /// the ticket branch; this is the run detail's copy. Null when the run derived
    /// no spec, and the dashboard then shows only the plan.
    /// </summary>
    public Task<string?> GetSpecMarkdown(string runId) =>
        specReader.ReadAsync(runId, Context.ConnectionAborted);

    /// <summary>
    /// p0243: returns the run's analyze.md from the artifact-store cache (24h TTL)
    /// — the analyzer's ProjectMap rendered as markdown, so the dashboard can show
    /// what the Analyze step understood. Null when the run is unknown, the cache
    /// has expired, or no analysis was cached; the dashboard hides the panel.
    /// </summary>
    public Task<string?> GetAnalyzeMarkdown(string runId) =>
        analyzeReader.ReadAsync(runId, Context.ConnectionAborted);
}
