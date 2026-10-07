using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-06-03c7c: rewrites the ids a draft's YAML TEXT states — its top-level <c>spec:</c>
/// scalar and the items of its <c>requires:</c> list — so the text a filing stores says what the
/// re-id'd draft says. An id matches by prefix: <c>p0012a-label</c> is rewritten as <c>p0012a</c>
/// with its label kept. Free-text preconditions are left as they were.
/// </summary>
public sealed class PhaseDraftIdRewriter
{
    private const string SpecKey = "spec:";
    private const string RequiresKey = "requires:";

    public string Rewrite(string yaml, IReadOnlyDictionary<string, string> ids)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        if (ids.Count == 0) return yaml;
        var token = TokenRegex(ids.Keys);
        var lines = yaml.Split('\n');
        var inRequires = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (IsTopLevelKey(lines[i])) inRequires = lines[i].StartsWith(RequiresKey, StringComparison.Ordinal);
            if (inRequires || lines[i].StartsWith(SpecKey, StringComparison.Ordinal))
                lines[i] = token.Replace(lines[i], m => ids[m.Value]);
        }
        return string.Join('\n', lines);
    }

    /// <summary>One id-bearing value — a draft's id or one requires entry — rewritten by the same rule.</summary>
    public string RewriteValue(string value, IReadOnlyDictionary<string, string> ids) =>
        ids.Count == 0 ? value : TokenRegex(ids.Keys).Replace(value, m => ids[m.Value]);

    // A top-level key starts in column 0 and is neither a list item nor a comment.
    private static bool IsTopLevelKey(string line) =>
        line.Length > 0 && !char.IsWhiteSpace(line[0]) && line[0] != '-' && line[0] != '#'
        && line.Contains(':', StringComparison.Ordinal);

    // Longest first, and never inside a longer token: p0012a must not match in p0012ab or xp0012a.
    private static Regex TokenRegex(IEnumerable<string> ids) =>
        new("(?<![A-Za-z0-9-])(?:"
            + string.Join("|", ids.OrderByDescending(id => id.Length).Select(Regex.Escape))
            + ")(?![0-9a-z])");
}
