using AgentSmith.Contracts.Events;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: turns the recorded trail into the two SERIES the story view draws — the model
/// calls in call order and the sandbox commands with their exit codes.
/// <para>
/// A series is not a statistic: folding these to totals is exactly what hides the shape
/// the operator diagnosed run 26 by. Both keep the order the run produced them in.
/// </para>
/// </summary>
public static class RunWorkPoints
{
    public static IReadOnlyList<RunCallPoint> Calls(
        IEnumerable<RunEvent> events, Func<int?, string?> phaseOf)
    {
        var index = 0;
        return [.. events.OfType<LlmCallFinishedEvent>().Select(c => new RunCallPoint(
            ++index,
            phaseOf(c.OriginStepIndex),
            c.OriginStepIndex,
            c.Role,
            c.Model,
            c.Measure.InputChars,
            c.Measure.OutputChars,
            c.Measure.DurationMs,
            c.ThrottleWaitMs,
            c.Measure.Outcome.ToString(),
            c.Measure.Attempt))];
    }

    public static IReadOnlyList<RunCommandPoint> Commands(
        IEnumerable<RunEvent> events, Func<int?, string?> phaseOf)
    {
        var index = 0;
        return [.. events.OfType<SandboxResultEvent>().Select(r => new RunCommandPoint(
            ++index,
            phaseOf(r.OriginStepIndex),
            r.OriginStepIndex,
            r.Repo,
            r.Command,
            r.ExitCode,
            r.Measure.DurationMs,
            r.Measure.OutputChars,
            r.Measure.DeliveredChars,
            r.Measure.Attempt))];
    }
}
