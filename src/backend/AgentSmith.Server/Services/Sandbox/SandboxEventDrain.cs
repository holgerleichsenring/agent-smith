using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0491: the read cursor over one sandbox job's event stream, and the reason a step's
/// output can no longer be left behind.
///
/// <para>The cursor is per JOB, not per step: entries read while waiting for one step are
/// gone for every later step, which discards them for carrying another step's id. The drain
/// therefore reads until the stream is EXHAUSTED rather than one page per poll tick. A
/// single-page drain let a chatty command outrun the reader — the agent writes one entry per
/// output line — and the step returned as soon as its result popped, so the unread lines
/// were dropped and the channel stayed behind for every short command that followed. That is
/// what made a live sandbox report an empty <c>pwd</c> and <c>ls</c>.</para>
///
/// <para>Exhaustion is bounded: the agent trims the stream at
/// <see cref="StreamLimits.EventStreamMaxLength"/>, so a drain that has read that many
/// entries has read more than can exist and returns to the caller's poll loop, which owns
/// the step deadline and the cancellation token. A producer that never quiesces costs an
/// extra page of reads per tick, never a hang.</para>
/// </summary>
internal sealed class SandboxEventDrain(IDatabase database, string streamKey, ILogger logger)
{
    private const int ReadPageSize = 100;
    private const int MaxPages = StreamLimits.EventStreamMaxLength / ReadPageSize;

    private string _lastSeenXid = "0-0";

    /// <summary>
    /// Forwards every entry belonging to <paramref name="stepId"/> that the stream holds,
    /// advancing the cursor past everything read.
    /// </summary>
    public async Task DrainAsync(Guid stepId, IProgress<StepEvent>? progress)
    {
        for (var page = 0; page < MaxPages; page++)
        {
            var entries = await database.StreamReadAsync(streamKey, _lastSeenXid, count: ReadPageSize);
            foreach (var entry in entries)
            {
                _lastSeenXid = entry.Id!;
                if (progress is not null) ForwardMatchingEvent(entry, stepId, progress);
            }
            if (entries.Length < ReadPageSize) return;
        }

        logger.LogWarning(
            "Sandbox event stream {Key} still had entries after {Max} — the drain stopped at "
            + "its bound and resumes on the next poll", streamKey, StreamLimits.EventStreamMaxLength);
    }

    private void ForwardMatchingEvent(StreamEntry entry, Guid stepId, IProgress<StepEvent> progress)
    {
        try
        {
            var raw = entry["data"];
            if (raw.IsNullOrEmpty) return;
            var ev = JsonSerializer.Deserialize<StepEvent>((string)raw!, WireFormat.Json);
            if (ev is not null && ev.StepId == stepId) progress.Report(ev);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize StepEvent {Id}", entry.Id);
        }
    }
}
