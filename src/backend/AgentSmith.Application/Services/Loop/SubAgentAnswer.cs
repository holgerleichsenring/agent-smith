namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-09-0620e: the child's final answer as its master receives it in the spawn result. 2026-11-10-14a3a: whole.
/// A part of an answer is never useful — the master cannot tell what is missing, and conversation 43's master never
/// read past a 12000-character cut. How long an answer is, is the child's job: its situation says what a good one
/// holds. Only a runaway past <see cref="Ceiling"/> is cut, and then the entry says so first.
/// </summary>
internal static class SubAgentAnswer
{
    /// <summary>A safety ceiling against a runaway, far above what any answer the situation asks for reaches.</summary>
    internal const int Ceiling = 60000;

    /// <summary>The ceiling as tool descriptions write it; a test keeps the two equal.</summary>
    internal const string CeilingText = "60000";

    /// <summary>How long a good answer is, in the words the situation tells a child.</summary>
    internal const int AimWords = 1500;

    /// <summary>2026-11-10-85e2: what one spawn call's answers may carry together — twenty runaways would not fit a prompt.</summary>
    internal const int CallCeiling = 200000;

    /// <summary>How an answer cut by the call's ceiling begins, so the spawn result can say so first.</summary>
    internal const string CallCutPrefix = "[CALL CEILING — ";

    /// <summary>The line a spawn result opens with when the call's ceiling cut an answer.</summary>
    internal const string CallCutLine = "This call's answers passed the ceiling together; the entries marked CALL CEILING are cut.";

    internal static bool IsRunaway(string? answer) => (answer?.Trim().Length ?? 0) > Ceiling;

    /// <summary>The answers in order, each whole while the call's ceiling allows; past it, cut and marked first.</summary>
    internal static IReadOnlyList<SubAgentResult> WithinCall(IReadOnlyList<SubAgentResult> results, out bool cut)
    {
        var left = CallCeiling;
        cut = false;
        var kept = new List<SubAgentResult>(results.Count);
        foreach (var result in results)
        {
            var answer = result.Answer ?? string.Empty;
            if (answer.Length <= left) { left -= answer.Length; kept.Add(result); continue; }
            cut = true;
            kept.Add(result with
            {
                Answer = $"{CallCutPrefix}this call's answers together passed {CallCeiling} characters; this one ran to "
                    + $"{answer.Length} and only the first {left} are shown]\n" + answer[..left],
            });
            left = 0;
        }
        return kept;
    }

    internal static string Whole(string? answer)
    {
        var text = answer?.Trim() ?? string.Empty;
        if (text.Length <= Ceiling) return text;
        return $"[RUNAWAY — this answer ran to {text.Length} characters and only the first {Ceiling} are shown; "
            + "what follows is incomplete]\n" + text[..Ceiling];
    }
}
