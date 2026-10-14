using System.Globalization;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-13-8762p: an overruled default as an act on its ticket. Like a Retry, the stored verdict IS the act
/// — read off the attempt, counted while it may be after what the attempt read. The newest such overrule names
/// the act; its text names every one of them, so a follow-up of a closed ticket carries each note.
/// </summary>
public static class OverruleAct
{
    public static IReadOnlyList<DecidedDefault> Since(PreviousAttempt? attempt) =>
        attempt is null ? [] : [.. attempt.Overrules.Where(o => attempt.Precedes(o.At)).OrderBy(o => o.At).ThenBy(o => o.Id)];

    public static ReworkAct? Of(PreviousAttempt? attempt) =>
        Since(attempt) is [.., var newest]
            ? new ReworkAct(newest.By, newest.At, ReworkChannel.Overrule)
            {
                Source = new AttemptSource(AttemptSourceKinds.Default, Id: newest.Id.ToString(CultureInfo.InvariantCulture)),
            }
            : null;

    /// <summary>Every overrule the attempt may not have seen, one paragraph each; null when there is none.</summary>
    public static string? TextOf(PreviousAttempt? attempt) =>
        Since(attempt) is { Count: > 0 } overrules ? string.Join("\n\n", overrules.Select(Describe)) : null;

    public static string Describe(DecidedDefault overrule) =>
        $"Default #{overrule.Id} overruled by {overrule.By} at {overrule.At.ToString("u", CultureInfo.InvariantCulture)}"
        + $"{(overrule.SpecId is { } spec ? $" (spec {spec})" : string.Empty)}:\n"
        + $"Question: {overrule.Question}\nChosen by default: {overrule.Chosen}\n"
        + $"Note: {(string.IsNullOrWhiteSpace(overrule.Note) ? "(none)" : overrule.Note)}";
}
