using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Defensive blocklist for shell commands routed through <c>run_command</c>.
/// The sandbox container is the primary isolation boundary; this guard is
/// defense-in-depth so accidental LLM-emitted destructive commands fail fast
/// at the host before reaching the sandbox.
/// </summary>
public static class CommandGuard
{
    private static readonly string[] DestructiveTokens =
    [
        "rm", "rmdir", "unlink", "shred", "truncate", "dd"
    ];

    private static readonly Regex DestructiveTokenRegex = new(
        @"(?:^|[;&|`$(]|\s)\s*(" + string.Join("|", DestructiveTokens) + @")\b",
        RegexOptions.Compiled);

    /// <summary>Returns an error message if <paramref name="command"/> matches a
    /// destructive pattern, otherwise <c>null</c>.</summary>
    public static string? Check(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;

        var match = DestructiveTokenRegex.Match(command);
        if (match.Success)
            return $"Error: command contains blocked destructive token '{match.Groups[1].Value}' — agent-smith run_command blocklist (rm/rmdir/unlink/shred/truncate/dd). Use a non-destructive alternative or read-only inspection.";

        if (command.Contains(":(){"))
            return "Error: fork-bomb pattern blocked.";

        if (Regex.IsMatch(command, @">\s*/dev/(?!null\b|stdout\b|stderr\b|tty\b)"))
            return "Error: raw device-write redirect blocked.";

        return null;
    }
}
