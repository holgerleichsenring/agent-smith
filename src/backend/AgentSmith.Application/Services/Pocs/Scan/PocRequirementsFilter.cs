using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the scanned copy of a requirements file holds only its exact pins ('name==version', extras,
/// markers and hashes kept, continuation lines joined); every other line — 'flask', 'requests&gt;=2.0', '-r', an index
/// option, a URL — is left out and named, so no scanner guesses a version for it and no option is ever followed. The
/// lines are data only: nothing is resolved or installed. A URL's credentials are not repeated in the names.
/// </summary>
public sealed partial class PocRequirementsFilter
{
    public (string Pinned, IReadOnlyList<string> LeftOut) Filter(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pinned = new List<string>();
        var leftOut = new List<string>();
        foreach (var line in Logical(text))
        {
            var content = Comment().Replace(line, string.Empty).Trim();
            if (content.Length == 0) continue;
            if (ExactPin().IsMatch(content)) pinned.Add(content);
            else leftOut.Add(Credentials().Replace(content, "//<credentials>@"));
        }
        return (string.Join('\n', pinned) + (pinned.Count > 0 ? "\n" : string.Empty), leftOut);
    }

    private static IEnumerable<string> Logical(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\\\n", " ", StringComparison.Ordinal).Split('\n');

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*(\[[A-Za-z0-9._,\s-]*\])?\s*==\s*[A-Za-z0-9][A-Za-z0-9._+!-]*\s*(;[^#]*)?(\s+--hash=\S+)*\s*$")]
    private static partial Regex ExactPin();

    [GeneratedRegex(@"(^|\s)#.*$")]
    private static partial Regex Comment();

    [GeneratedRegex(@"//[^/@\s]+@")]
    private static partial Regex Credentials();
}
