namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-09-0620e: the part of a child's final answer its master receives in the spawn result. A longer answer is
/// cut, and the cut says how much is left and where it is. 2026-11-10-14a3a: the notice LEADS the entry — at the
/// end of 12000 characters it read like the answer's last line, and the one answer that mattered was never read on.
/// </summary>
internal static class SubAgentAnswer
{
    // 2026-11-09-0620m: live, four children answered 8.6k-13.5k characters and a 6000 cut sent the master back to
    // read all four whole; 12000 lets a full answer through and still bounds a runaway one.
    internal const int MaxChars = 12000;

    /// <summary>How a cut answer begins, so the spawn result can name it without a second flag.</summary>
    internal const string CutPrefix = "[CUT — ";

    internal static bool WasCut(string? condensed) => condensed?.StartsWith(CutPrefix, StringComparison.Ordinal) == true;

    internal static string Condensed(string? answer, string subAgentId)
    {
        var text = answer?.Trim() ?? string.Empty;
        if (text.Length <= MaxChars) return text;
        return $"{CutPrefix}the first {MaxChars} of {text.Length} characters; read_sub_agent_observations(\"{subAgentId}\") "
            + "returns the whole answer, and what follows here is incomplete]\n" + text[..MaxChars];
    }

    /// <summary>The line a spawn result opens with when any child's answer was cut; null when none was.</summary>
    internal static string? CutLine(IEnumerable<SubAgentResult> results)
    {
        var cut = results.Where(r => WasCut(r.Answer)).Select(r => $"{r.Name} ({r.SubAgentId})").ToList();
        return cut.Count == 0 ? null : $"Cut, not whole — read_sub_agent_observations returns the rest: {string.Join(", ", cut)}";
    }
}
