namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0423: the five questions every unit of work answers, whatever kind of unit it is —
/// a model call, a tool call, a sandbox command. When it ran (the event's own
/// Timestamp) plus how long it took, how much went in, how much came out both before
/// and after any bound, how it ended, and how many attempts it took.
/// <para>
/// Instrumenting the LAST failure instruments the last war: a field list justified by
/// "Prompt is too long" stops earning its keep the moment tool results are bounded.
/// These five outlive the failures they were found by, so a defect of a shape nobody
/// has seen yet still shows up as a number that moved.
/// </para>
/// <para>
/// It is a VIEW, never a second copy. Events that already carry a duration, an exit
/// code or a result length keep those fields as their own producers and consumers know
/// them; the measure is composed from them, so the two can never disagree.
/// </para>
/// </summary>
/// <param name="DurationMs">How long the unit took, wall clock.</param>
/// <param name="InputChars">Characters handed to the unit — prompt, arguments, command.</param>
/// <param name="OutputChars">Characters the unit produced, before any bound was applied.</param>
/// <param name="DeliveredChars">Characters that actually reached the model. Equal to
/// <paramref name="OutputChars"/> when nothing was cut.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Attempt">Which attempt this was; 1 = first, higher = it was retried.</param>
public sealed record WorkMeasure(
    long DurationMs,
    long InputChars,
    long OutputChars,
    long DeliveredChars,
    WorkOutcome Outcome,
    int Attempt = 1)
{
    /// <summary>How much of the unit's output never reached the model.</summary>
    public long DroppedChars => Math.Max(0, OutputChars - DeliveredChars);
}
