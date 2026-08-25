using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-08-25-61f1: what buffering one event yielded — the trail position the buffer
/// assigned it, and the batch that position completed (null while the batch is still
/// filling). The position leaves the buffer because it is the run event's IDENTITY:
/// every row the event produces carries it, which is what makes the recording idempotent.
/// </summary>
/// <param name="Seq">The event's position in this run's trail.</param>
/// <param name="ToFlush">The batch to write, or null when nothing is due yet.</param>
public readonly record struct TrailAddition(
    long Seq, IReadOnlyList<(long Seq, RunEvent Event)>? ToFlush);
