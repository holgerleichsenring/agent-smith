using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9d: which of a pull request's review counts as feedback for the next attempt. A
/// note counts when a trusted person wrote it (no bot, no system note); one of ours — 2026-10-08-f147:
/// marker AND the token's account (<see cref="PrReviewNote.IsOurs"/>), since a marker is text anyone
/// can paste — only when a person answered it later in the thread. A thread counts when it is unresolved, or has a counted note written after the previous
/// attempt started; a thread left with no notes is dropped.
/// </summary>
public static class PrReviewSelection
{
    public static IReadOnlyList<PrReviewThread> Select(
        IReadOnlyList<PrReviewThread> trustedThreads, PreviousAttempt attempt) =>
        [.. trustedThreads.Select(t => t with { Notes = Counted(t.Notes) })
            .Where(t => t.Notes.Count > 0 && (t.Resolved == false || t.Notes.Any(n => attempt.Precedes(n.At))))];

    private static IReadOnlyList<PrReviewNote> Counted(IReadOnlyList<PrReviewNote> notes)
    {
        var ordered = notes.OrderBy(n => n.At).ToList();
        var kept = new List<PrReviewNote>();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!IsOurs(ordered[i])) { kept.Add(ordered[i]); continue; }
            if (ordered.Skip(i + 1).Any(n => !IsOurs(n))) kept.Add(ordered[i]);
        }
        return kept;
    }

    public static bool IsOurs(PrReviewNote note) => note.IsOurs;
}
