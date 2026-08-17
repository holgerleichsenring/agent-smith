using AgentSmith.Contracts.Events;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423: the size and shape of a run's calls, DERIVED from its recorded units.
/// <para>
/// It is a fold over events, never a counter kept alongside them. A counter and the events
/// it counts are two answers to one question, and they drift — which is why the phase
/// asks for statistics that are queries. Group the events first (by phase, by role, by
/// step) and fold each group to get the same numbers per slice.
/// </para>
/// <para>
/// These are the numbers that named the wall: the prompt grew 151k -> 216k -> 278k -> 341k
/// while the answers shrank 3,886 -> 2,750 -> 969 -> 0 bytes. Both halves are here,
/// because either alone reads as noise.
/// </para>
/// </summary>
public sealed record RunCallStatistics(
    int Calls,
    int FailedCalls,
    long TotalDurationMs,
    long TotalPromptChars,
    long LargestPromptChars,
    long TotalResponseChars,
    long SmallestResponseChars,
    int ToolCalls,
    long ToolOutputChars,
    long ToolCharsNeverDelivered,
    int Retries)
{
    public static RunCallStatistics From(IEnumerable<RunEvent> events)
    {
        var calls = events.OfType<LlmCallFinishedEvent>().ToList();
        var tools = events.OfType<ToolResultEvent>().ToList();
        return new RunCallStatistics(
            Calls: calls.Count,
            FailedCalls: calls.Count(c => c.Measure.Outcome != WorkOutcome.Ok),
            TotalDurationMs: calls.Sum(c => c.Measure.DurationMs),
            TotalPromptChars: calls.Sum(c => c.Measure.InputChars),
            LargestPromptChars: calls.Count == 0 ? 0 : calls.Max(c => c.Measure.InputChars),
            TotalResponseChars: calls.Sum(c => c.Measure.OutputChars),
            SmallestResponseChars: calls.Count == 0 ? 0 : calls.Min(c => c.Measure.OutputChars),
            ToolCalls: tools.Count,
            ToolOutputChars: tools.Sum(t => t.Measure.OutputChars),
            ToolCharsNeverDelivered: tools.Sum(t => t.Measure.DroppedChars),
            Retries: calls.Sum(c => c.Measure.Attempt - 1) + tools.Sum(t => t.Measure.Attempt - 1));
    }
}
