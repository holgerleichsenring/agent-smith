namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-09-0620e: the part of a child's final answer its master receives in the spawn result — about one to two
/// thousand tokens, the size Anthropic's guidance gives a sub-agent's condensed result. A longer answer is cut, and the
/// cut says how much is left and where it is.
/// </summary>
internal static class SubAgentAnswer
{
    internal const int MaxChars = 6000;

    internal static string Condensed(string? answer)
    {
        var text = answer?.Trim() ?? string.Empty;
        if (text.Length <= MaxChars) return text;
        return text[..MaxChars]
            + $"\n[… {text.Length - MaxChars} more characters — read_sub_agent_observations returns the whole answer]";
    }
}
