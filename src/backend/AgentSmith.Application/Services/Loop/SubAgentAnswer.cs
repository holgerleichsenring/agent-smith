namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-09-0620e: the part of a child's final answer its master receives in the spawn result. A longer answer is
/// cut, and the cut says how much is left and where it is.
/// </summary>
internal static class SubAgentAnswer
{
    // 2026-11-09-0620m: live, four children answered 8.6k-13.5k characters and a 6000 cut sent the master back to
    // read all four whole; 12000 lets a full answer through and still bounds a runaway one.
    internal const int MaxChars = 12000;

    internal static string Condensed(string? answer)
    {
        var text = answer?.Trim() ?? string.Empty;
        if (text.Length <= MaxChars) return text;
        return text[..MaxChars]
            + $"\n[… {text.Length - MaxChars} more characters — read_sub_agent_observations returns the whole answer]";
    }
}
