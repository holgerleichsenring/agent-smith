using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Events.Projection;

/// <summary>
/// 2026-10-26-0f19c: writes the events of one run the store may not hold yet — every stream entry after
/// <see cref="RunProjectionResumePoint"/> but sandbox output, which is never persisted — and returns the
/// highest position it read. A new holder's own drain cursor says what it showed its clients, not what
/// the previous holder wrote, so the store is the resume point. Writing an event again is absorbed by the
/// identity rows; a run with no entry to resume after returns <see cref="long.MinValue"/>.
/// </summary>
public sealed class RunProjectionCatchUp(
    IConnectionMultiplexer redis, RunProjectionResumePoint resume, EventEnvelopeSerializer envelopes)
{
    private const int Page = 100;

    public async Task<long> RunAsync(string runId, IRunEventPersistence into, CancellationToken ct)
    {
        if (await resume.AfterAsync(runId, ct) is not { } after) return long.MinValue;
        var through = after == RunProjectionResumePoint.FromStart ? long.MinValue : StreamTrailPosition.Of(after);
        var db = redis.GetDatabase();
        while (true)
        {
            var entries = await db.StreamReadAsync(EventStreamKeys.RunStream(runId), after, count: Page);
            if (entries.Length == 0) return through;
            foreach (var entry in entries)
            {
                after = entry.Id.ToString();
                through = StreamTrailPosition.Of(after);
                if (Read(entry) is { Type: not EventType.SandboxOutput } runEvent)
                    await into.PersistAsync(runEvent, through, ct);
            }
        }
    }

    private RunEvent? Read(StreamEntry entry)
    {
        foreach (var pair in entry.Values)
        {
            if (pair.Value.IsNullOrEmpty) continue;
            try { return envelopes.Deserialize(pair.Value.ToString()); }
            catch { return null; }
        }
        return null;
    }
}
