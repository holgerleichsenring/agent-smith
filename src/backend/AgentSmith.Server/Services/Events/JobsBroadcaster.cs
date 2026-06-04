using System.Collections.Concurrent;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Long-running broadcaster: subscribes unconditionally to every per-run
/// stream, maintains the active + recent in-memory snapshots, fans events
/// out to SignalR groups via <see cref="IRunEventFanout"/>. Cold-starts
/// from the two pointer indices (active SET + recent LIST), never KEYS /
/// SCAN. SandboxOutput L3 events fan out only when an ExpandSandbox group
/// is active for (runId, repo).
/// </summary>
public sealed class JobsBroadcaster(
    IConnectionMultiplexer redis,
    IRunEventFanout fanout,
    SandboxExpansionRegistry expansionRegistry,
    ILogger<JobsBroadcaster> logger) : IHostedService, IAsyncDisposable
{
    private const int RecentCapacity = EventStreamKeys.RecentRunsCap;
    // p0175-fix: bumped from 500 → 10_000 to match the Redis system stream
    // MAXLEN. The 500-cap was too tight for active trackers — one poller
    // pushing ~94 events/cycle (started + finished + ~46 scanned + ~46
    // skipped) exhausted the buffer in 5-6 cycles. The 24h rollup then
    // diverged from the visible cycle list because the oldest
    // PollCycleStarted got evicted while its matching Finished did not.
    private const int SystemRecentCapacity = 10_000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly ConcurrentDictionary<string, RunSnapshot> _active = new(StringComparer.Ordinal);
    private readonly RecentRunsRingBuffer _recent = new(RecentCapacity);
    private readonly SystemRecentRingBuffer _systemRecent = new(SystemRecentCapacity);
    private readonly ConcurrentDictionary<string, string> _streamCursors = new(StringComparer.Ordinal);
    private string _systemCursor = "0-0";
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public IReadOnlyDictionary<string, RunSnapshot> Active => _active;
    public IReadOnlyList<RunSnapshot> Recent => _recent.Snapshot();
    public IReadOnlyList<SystemEvent> SystemRecent => _systemRecent.Snapshot();

    /// <summary>
    /// p0175-fix: 24h rolling aggregate computed from the in-memory system
    /// ring buffer. Cheap O(N) over <see cref="SystemRecentCapacity"/>; called
    /// on each subscribe and re-broadcast after every system event publish.
    /// </summary>
    public SystemActivitySnapshot GetSystemActivity() =>
        SystemActivitySnapshot.Compute(_systemRecent.Snapshot(), DateTimeOffset.UtcNow);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ColdStartAsync(cancellationToken);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        logger.LogInformation(
            "JobsBroadcaster started: {ActiveCount} active runs, {RecentCount} recent runs",
            _active.Count, _recent.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null) return;
        _cts.Cancel();
        try { if (_loop is not null) await _loop; }
        catch (OperationCanceledException) { }
        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));

    private async Task ColdStartAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await RehydrateActiveAsync(db, ct);
        await RehydrateRecentAsync(db, ct);
        await RehydrateSystemRecentAsync(db, ct);
    }

    // p0173a: cold-start populates the system ring buffer from the newest
    // SystemRecentCapacity entries of the stream so the dashboard /system
    // view is immediately useful after a server restart.
    private async Task RehydrateSystemRecentAsync(IDatabase db, CancellationToken ct)
    {
        if (!await db.KeyExistsAsync(SystemEventStreamKeys.Stream)) return;
        var entries = await db.StreamRangeAsync(
            SystemEventStreamKeys.Stream, "-", "+", SystemRecentCapacity, Order.Descending);
        // Order.Descending returns newest-first; reverse to append chronologically.
        for (var i = entries.Length - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            var systemEvent = DeserializeSystemEntry(entries[i]);
            if (systemEvent is null) continue;
            _systemRecent.Append(systemEvent);
        }
        // After cold-start, live drain reads from `$` so we don't replay
        // the same entries through the fanout.
        _systemCursor = "$";
    }

    private async Task RehydrateActiveAsync(IDatabase db, CancellationToken ct)
    {
        var members = await db.SetMembersAsync(EventStreamKeys.ActiveRunsSet);
        foreach (var member in members)
        {
            ct.ThrowIfCancellationRequested();
            var runId = member.ToString();
            var snapshot = await RehydrateFromStreamAsync(db, runId);
            if (snapshot is null)
            {
                await db.SetRemoveAsync(EventStreamKeys.ActiveRunsSet, runId);
                continue;
            }
            _active[runId] = snapshot;
            _streamCursors[runId] = "$";
        }
    }

    private async Task RehydrateRecentAsync(IDatabase db, CancellationToken ct)
    {
        var members = await db.ListRangeAsync(EventStreamKeys.RecentRunsList, 0, -1);
        foreach (var member in members)
        {
            ct.ThrowIfCancellationRequested();
            var runId = member.ToString();
            var snapshot = await RehydrateFromStreamAsync(db, runId);
            if (snapshot is null)
            {
                await db.ListRemoveAsync(EventStreamKeys.RecentRunsList, runId);
                continue;
            }
            _recent.Upsert(snapshot);
        }
    }

    private static async Task<RunSnapshot?> RehydrateFromStreamAsync(IDatabase db, string runId)
    {
        var key = EventStreamKeys.RunStream(runId);
        if (!await db.KeyExistsAsync(key)) return null;
        // p0225: fold the FULL stream in chronological order to rebuild the
        // complete snapshot. Title/repos/pipeline/agent live on the EARLY
        // RunStarted + TicketFetched events; replaying only the head (newest)
        // event applied just e.g. RunFinished to an empty seed, so every
        // rehydrated run rendered as "unknown · no repos · 0s" — even successful
        // ones. Recent is capped, so this cold-start fold is bounded.
        var entries = await db.StreamRangeAsync(key, "-", "+");
        if (entries.Length == 0) return RunSnapshot.Empty(runId);
        return RebuildSnapshot(runId, entries.Select(DeserializeEntry));
    }

    // p0225: pure fold extracted so the rebuild is unit-testable without Redis.
    public static RunSnapshot RebuildSnapshot(string runId, IEnumerable<RunEvent?> events) =>
        events.Where(e => e is not null)
              .Aggregate(RunSnapshot.Empty(runId), (snapshot, e) => snapshot.Apply(e!));

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await DiscoverNewRunsAsync(db, ct);
                await DrainAsync(db, ct);
                await DrainSystemAsync(db, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "JobsBroadcaster loop hit transient error");
            }
            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DiscoverNewRunsAsync(IDatabase db, CancellationToken ct)
    {
        var members = await db.SetMembersAsync(EventStreamKeys.ActiveRunsSet);
        foreach (var member in members)
        {
            ct.ThrowIfCancellationRequested();
            var runId = member.ToString();
            _streamCursors.TryAdd(runId, "0-0");
            _active.GetOrAdd(runId, RunSnapshot.Empty);
        }
    }

    private async Task DrainAsync(IDatabase db, CancellationToken ct)
    {
        foreach (var (runId, cursor) in _streamCursors.ToArray())
        {
            ct.ThrowIfCancellationRequested();
            var key = EventStreamKeys.RunStream(runId);
            var fromCursor = cursor == "$" ? "0-0" : cursor;
            var entries = await db.StreamReadAsync(key, fromCursor, count: 100);
            if (entries.Length == 0) continue;
            foreach (var entry in entries)
            {
                var runEvent = DeserializeEntry(entry);
                if (runEvent is null) continue;
                await ProcessEventAsync(runId, runEvent, ct);
            }
            _streamCursors[runId] = entries[^1].Id.ToString();
        }
    }

    private async Task ProcessEventAsync(string runId, RunEvent runEvent, CancellationToken ct)
    {
        if (runEvent.Type == EventType.SandboxOutput && runEvent is SandboxOutputEvent so)
        {
            if (expansionRegistry.IsExpanded(runId, so.Repo))
                await fanout.ToSandboxAsync(runId, so.Repo, so, ct);
            return;
        }

        var snapshot = _active.AddOrUpdate(
            runId,
            _ => RunSnapshot.Empty(runId).Apply(runEvent),
            (_, existing) => existing.Apply(runEvent));

        await fanout.ToOverviewAsync(snapshot, ct);
        await fanout.ToRunAsync(runId, runEvent, ct);

        if (runEvent.Type == EventType.RunFinished)
        {
            _active.TryRemove(runId, out _);
            _recent.Upsert(snapshot);
            _streamCursors.TryRemove(runId, out _);
        }
    }

    private static RunEvent? DeserializeEntry(StreamEntry entry)
    {
        foreach (var pair in entry.Values)
        {
            var payload = pair.Value.ToString();
            if (string.IsNullOrEmpty(payload)) continue;
            try { return EventEnvelopeSerializer.Deserialize(payload); }
            catch { return null; }
        }
        return null;
    }

    private async Task DrainSystemAsync(IDatabase db, CancellationToken ct)
    {
        var fromCursor = _systemCursor == "$" ? "0-0" : _systemCursor;
        var entries = await db.StreamReadAsync(SystemEventStreamKeys.Stream, fromCursor, count: 100);
        if (entries.Length == 0) return;
        var appended = false;
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var systemEvent = DeserializeSystemEntry(entry);
            if (systemEvent is null) continue;
            _systemRecent.Append(systemEvent);
            appended = true;
            await fanout.ToSystemAsync(systemEvent, ct);
        }
        _systemCursor = entries[^1].Id.ToString();
        // p0175-fix: one rollup broadcast per batch (not per event) keeps the
        // SignalR overhead bounded under burst load while still keeping the
        // /system KPI cards within ~200ms of true state (the loop interval).
        if (appended) await fanout.ToSystemActivityAsync(GetSystemActivity(), ct);
    }

    private static SystemEvent? DeserializeSystemEntry(StreamEntry entry)
    {
        foreach (var pair in entry.Values)
        {
            var payload = pair.Value.ToString();
            if (string.IsNullOrEmpty(payload)) continue;
            try { return EventEnvelopeSerializer.DeserializeSystem(payload); }
            catch { return null; }
        }
        return null;
    }
}
