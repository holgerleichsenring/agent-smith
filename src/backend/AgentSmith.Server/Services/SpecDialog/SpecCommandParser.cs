using System.Text.RegularExpressions;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Parses the "/spec" chat command into a <see cref="SpecOpenCommand"/>.
/// Returns null for any text that is not a /spec command (normal chat).
/// <para>
/// 2026-09-22-2a86: the door, and nothing else. "list", "resume" and "fork" were spellings
/// only this parser built and only a person typed; the dashboard now resumes by a route, so
/// the first word after "/spec" is read as the project it always was for every other word.
/// </para>
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

        return new SpecOpenCommand(args.Split(' ', 2, StringSplitOptions.TrimEntries)[0]);
    }
}
