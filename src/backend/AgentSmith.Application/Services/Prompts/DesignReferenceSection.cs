using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Design;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-01-7f7ad: the "Design references" prompt section — the Figma links the ticket, its
/// comments or the conversation point at, OFFERED to the model and never fetched. Each link is
/// REBUILT from the file key and node id <see cref="FigmaLink"/> parsed against fixed alphabets,
/// so the section carries no ticket-authored character and stands outside the delimiters; the
/// original link stays where it already is, inside the delimited ticket text. De-duplicated by
/// key, branch and node; at most ten listed, the remainder counted. Empty when there is none.
/// </summary>
public static partial class DesignReferenceSection
{
    private const int MaxListed = 10;

    public static string Render(IEnumerable<string?> texts, bool readable)
    {
        var links = texts.SelectMany(LinksIn).Distinct().ToList();
        if (links.Count == 0)
            return string.Empty;

        var sb = new StringBuilder("\n\n## Design references\n");
        sb.AppendLine(readable
            ? "The ticket or conversation points at these Figma designs, rebuilt from their file key and "
              + "node. Nothing has been read: read a frame with design_read when the work depends on it."
            : "The ticket or conversation points at these Figma designs, rebuilt from their file key and "
              + "node. No design source is configured for this project, so they cannot be read here — "
              + "web_fetch on a Figma link returns the application shell, not the design. Say so when "
              + "the work depends on one.");
        foreach (var link in links.Take(MaxListed))
            sb.AppendLine(link.NodeId is null
                ? $"- {link.Canonical} (names no node: design_read needs the link of a frame)"
                : $"- {link.Canonical}");
        if (links.Count > MaxListed)
            sb.AppendLine($"- and {links.Count - MaxListed} more not listed.");
        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<FigmaLink> LinksIn(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (Match match in Candidate().Matches(text))
            if (FigmaLink.TryParse(WebUtility.HtmlDecode(match.Value).TrimEnd('.', ',', ';', ':', '!', '?'), out var link))
                yield return link;
    }

    [GeneratedRegex("""https://[^\s<>"'()\[\]{}|]+""")]
    private static partial Regex Candidate();
}
