using System.Text.RegularExpressions;

namespace AgentSmith.Application.Webhooks;

/// <summary>
/// The structural mark of a PR/MR comment command: a line opening with <c>/agent-smith</c>,
/// <c>/agentsmith</c> or <c>/as</c>, followed by whitespace or the end of the comment. The tail is
/// everything after the prefix, every following line included; an empty tail is a bare command.
/// <c>/as-is</c> and <c>/assign</c> are no commands. One reading for the admission and for the
/// rework act reader, so a comment that started a rework is the comment the run serves.
/// </summary>
public static partial class CommentCommandPrefix
{
    /// <summary>The trimmed tail of the command, empty for a bare one; null when the body is no command.</summary>
    public static string? TailOf(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var match = CommandRegex().Match(body);
        return match.Success ? match.Groups["tail"].Value.Trim() : null;
    }

    public static bool IsCommand(string? body) => TailOf(body) is not null;

    // Slash-prefix discrimination is intentionally a regex: operators expect a deterministic
    // "this is a command" marker. The tail capture passes everything after the prefix verbatim.
    [GeneratedRegex(@"^/(?:agent-smith|agentsmith|as)(?=\s|$)(?<tail>(?s:.*))", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CommandRegex();
}
