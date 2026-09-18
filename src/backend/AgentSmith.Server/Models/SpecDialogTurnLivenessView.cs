namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-18-2f8b: what the turn on this conversation is doing, as a page arriving mid-turn
/// reads it. <paramref name="Computing"/> is true only while a turn is actually computing —
/// a turn blocked on its own question is waiting on a PERSON, and that wait has no deadline
/// at all, so a working line beside the card would claim the agent is thinking about an
/// answer nobody has given yet.
/// <para>
/// <paramref name="ElapsedSeconds"/> is the time the turn has COMPUTED, measured at read time
/// on the server with the waits on a person left out, and the page counts up from it. Two
/// clocks are never differenced, so a browser running behind cannot render a turn that
/// started in the future.
/// </para>
/// </summary>
/// <param name="Steps">What the turn has reported so far, oldest first, each under the
/// sequence the page merges the live pushes against.</param>
/// <param name="TurnStartedAt">The turn's identity: a sequence alone repeats every turn, so a
/// page that missed the reply between two turns would filter the new turn's steps out as
/// duplicates of the dead one's. Null when no turn is computing.</param>
public sealed record SpecDialogTurnLivenessView(
    bool Computing,
    int ElapsedSeconds,
    IReadOnlyList<SpecDialogActivityPush> Steps,
    DateTimeOffset? TurnStartedAt)
{
    /// <summary>No turn is computing: nothing to show and no clock to count.</summary>
    public static SpecDialogTurnLivenessView Idle { get; } = new(false, 0, [], null);
}
