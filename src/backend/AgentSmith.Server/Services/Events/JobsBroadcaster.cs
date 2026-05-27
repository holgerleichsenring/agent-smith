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
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly ConcurrentDictionary<string, RunSnapshot> _active = new(StringComparer.Ordinal);
    private readonly RecentRunsRingBuffer _recent = new(RecentCapacity);
    private readonly ConcurrentDictionary<string, string> _streamCursors = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public IReadOnlyDictionary<string, RunSnapshot> Active => _active;
    public IReadOnlyList<RunSnapshot> Recent => _recent.Snapshot();

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
        var entries = await db.StreamRangeAsync(key, "-", "+", 1, Order.Descending);
        if (entries.Length == 0) return RunSnapshot.Empty(runId);
        var head = DeserializeEntry(entries[0]);
        return head is null ? RunSnapshot.Empty(runId) : RunSnapshot.Empty(runId).Apply(head);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await DiscoverNewRunsAsync(db, ct);
                await DrainAsync(db, ct);
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
}
