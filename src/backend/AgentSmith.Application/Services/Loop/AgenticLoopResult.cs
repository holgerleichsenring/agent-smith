using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: output of one <see cref="IAgenticLoopRunner.RunAsync"/> call.
/// The chat response and elapsed duration; cost is tracked externally by
/// the caller via <c>PipelineCostTracker.Track</c> so the master + child
/// agents accrue against the shared per-run tracker.
/// </summary>
public sealed record AgenticLoopResult(
    ChatResponse Response,
    TimeSpan Duration);
