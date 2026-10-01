using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Models.Design;

/// <summary>
/// 2026-10-01-7f7ab: a Figma link reduced to what the REST API is addressed by — the file key,
/// the branch key when the link points into a branch, and the node id. Only https links on
/// figma.com or www.figma.com whose path is /file, /design or /proto parse; anything else is
/// refused before a request exists. Key and node are parsed against fixed alphabets, so a
/// parsed link carries no free text.
/// </summary>
public sealed partial record FigmaLink(string FileKey, string? BranchKey, string? NodeId)
{
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
        { "figma.com", "www.figma.com" };

    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal)
        { "file", "design", "proto" };

    /// <summary>The key the API reads: a branch is addressed by its own key.</summary>
    public string ApiFileKey => BranchKey ?? FileKey;

    /// <summary>Parses <paramref name="text"/>; false for any link this type does not accept.</summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out FigmaLink? link)
    {
        link = null;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !Hosts.Contains(uri.Host) || !uri.IsDefaultPort)
            return false;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !Kinds.Contains(segments[0]) || !KeyPattern().IsMatch(segments[1]))
            return false;
        var branch = segments.Length >= 4 && segments[2] == "branch" ? segments[3] : null;
        if (branch is not null && !KeyPattern().IsMatch(branch)) return false;
        var node = NodeOf(uri.Query);
        if (node is { Length: 0 }) return false;
        link = new FigmaLink(segments[1], branch, node);
        return true;
    }

    // Null when the link names no node; empty when it names one this type cannot read.
    private static string? NodeOf(string query)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || parts[0] != "node-id") continue;
            var value = Uri.UnescapeDataString(parts[1]);
            return NodePattern().IsMatch(value) ? value.Replace('-', ':') : string.Empty;
        }
        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9]{6,64}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^I?[0-9]+[-:][0-9]+(;[0-9]+[-:][0-9]+)*$")]
    private static partial Regex NodePattern();
}
