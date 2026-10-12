using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7f: the hand-back of a series whose repositories carry different revisions of it.
/// The reason names the lagging repositories; the comment names both ways out — a re-cut demand with
/// the ticket moved back to a trigger status, which cuts the series again over every repository and
/// keeps what already ran, or a new approval filed again. Its heading carries <see cref="Marker"/>, so
/// the next run keeps it in view while nobody has answered.
/// </summary>
public static class SeriesOutOfStepComment
{
    /// <summary>The phrase that marks an out-of-step comment as still awaiting an answer.</summary>
    public const string Marker = "the series is out of step between its repositories";

    /// <summary>The hand-back for a read whose <paramref name="lagging"/> members lag the newest revision.</summary>
    public static SpecHandback Handback(string key, IReadOnlyList<string> lagging, IReadOnlyCollection<string> current) =>
        new(SpecHandbackCase.SeriesOutOfStep,
            $"The series of {key} is out of step: {string.Join(", ", lagging)} "
            + $"{(lagging.Count == 1 ? "lags" : "lag")} the newest revision, which "
            + $"{string.Join(", ", current)} {(current.Count == 1 ? "carries" : "carry")}.");

    public static string Build(SpecHandback handback)
    {
        ArgumentNullException.ThrowIfNull(handback);
        return $"## Agent Smith — {Marker}\n\n"
            + handback.Reason
            + "\n\nNothing was derived and nothing ran: working from a mix of revisions would build what "
            + "nobody approved. Two ways out:\n"
            + $"- Write a comment that opens with `{SpecSetComment.RecutDemand}` on a line of its own AND move "
            + "the ticket back to a trigger status — the next run cuts the series again over every repository "
            + "and keeps the specs that already ran.\n"
            + "- Or approve the specification again in the design conversation and file the ticket again.";
    }
}
