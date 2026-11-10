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

    /// <summary>How long a good answer is, in the words the situation tells a child.</summary>
    internal const int AimWords = 1500;

    internal static bool IsRunaway(string? answer) => (answer?.Trim().Length ?? 0) > Ceiling;

    internal static string Whole(string? answer)
    {
        var text = answer?.Trim() ?? string.Empty;
        if (text.Length <= Ceiling) return text;
        return $"[RUNAWAY — this answer ran to {text.Length} characters and only the first {Ceiling} are shown; "
            + "what follows is incomplete]\n" + text[..Ceiling];
    }
}
