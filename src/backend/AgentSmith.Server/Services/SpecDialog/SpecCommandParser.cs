using System.Text.RegularExpressions;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Parses "/spec" chat commands into typed <see cref="SpecCommand"/> instances.
/// Returns null for any text that is not a /spec command (normal chat).
/// </summary>
public sealed class SpecCommandParser
{
    private static readonly Regex SpecPattern = new(
        @"^/spec(?:\s+(?<args>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    public SpecCommand? Parse(string text)
    {
        var match = SpecPattern.Match(text.Trim());
        if (!match.Success) return null;

        var args = match.Groups["args"].Value.Trim();
        if (args.Length == 0) return new SpecOpenCommand(Project: null);

        var parts = args.Split(' ', 2, StringSplitOptions.TrimEntries);
        return parts[0].ToLowerInvariant() switch
        {
            "list" => new SpecListCommand(),
            "resume" => new SpecResumeCommand(parts.Length == 2 ? parts[1] : string.Empty),
            "new" => new SpecNewCommand(parts.Length == 2 ? parts[1] : null),
            _ => new SpecOpenCommand(parts[0]),
        };
    }
}
