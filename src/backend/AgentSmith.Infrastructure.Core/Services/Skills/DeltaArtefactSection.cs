using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// 2026-09-15-d66f: reads a language delta's <c>## Artefacts</c> section — the enforcement files
/// it declares — and hands back the delta WITHOUT that section.
/// <para>
/// Both halves matter. The entries are what the transfer writes into a repository; the removal is
/// what keeps every stack's composed principles byte-identical to before the section existed,
/// because the composer inlines the delta file verbatim and a mandatory section would otherwise
/// change the output of every stack, including the two that declare nothing.
/// </para>
/// <para>
/// One entry per <c>### &lt;path&gt;</c> heading, whose content is the first fenced block under it.
/// A heading with no fenced block declares nothing and is skipped rather than guessed at — a
/// section stated empty ("No artefacts — ...") carries no headings at all and yields none.
/// </para>
/// </summary>
internal static class DeltaArtefactSection
{
    private const string Heading = "## Artefacts";
    private const string EntryPrefix = "### ";
    private const string Fence = "```";

    /// <summary>
    /// The delta with its artefacts section removed, and the artefacts it declared. A delta that
    /// has no such section comes back unchanged with an empty list.
    /// </summary>
    internal static (string Delta, IReadOnlyList<PrinciplesArtefact> Artefacts) Split(string delta)
    {
        var lines = delta.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, l => l.TrimEnd() == Heading);
        if (start < 0) return (delta, []);

        // The section runs to the next top-level heading, or to the end of the file.
        var end = Array.FindIndex(lines, start + 1, l => l.StartsWith("## ", StringComparison.Ordinal));
        if (end < 0) end = lines.Length;

        var body = lines[start..end];
        var remaining = lines[..start].Concat(lines[end..]);
        return (string.Join("\n", remaining).TrimEnd(), Parse(body));
    }

    private static List<PrinciplesArtefact> Parse(string[] body)
    {
        var artefacts = new List<PrinciplesArtefact>();
        for (var i = 0; i < body.Length; i++)
        {
            if (!body[i].StartsWith(EntryPrefix, StringComparison.Ordinal)) continue;
            var path = body[i][EntryPrefix.Length..].Trim();
            if (path.Length == 0) continue;

            var content = FencedBlockAfter(body, i, out var consumed);
            if (content is null) continue;
            artefacts.Add(new PrinciplesArtefact(path, content));
            i = consumed;
        }
        return artefacts;
    }

    /// <summary>
    /// The first fenced block between this entry's heading and the next one. Stopping at the next
    /// heading is what keeps a declared-but-empty entry from swallowing the following artefact's
    /// content and filing it under the wrong path.
    /// </summary>
    private static string? FencedBlockAfter(string[] body, int headingIndex, out int consumed)
    {
        consumed = headingIndex;
        var content = new StringBuilder();
        var inside = false;
        for (var i = headingIndex + 1; i < body.Length; i++)
        {
            var line = body[i];
            if (!inside && line.StartsWith(EntryPrefix, StringComparison.Ordinal)) return null;
            if (line.StartsWith(Fence, StringComparison.Ordinal))
            {
                if (!inside) { inside = true; continue; }
                consumed = i;
                return content.ToString().TrimEnd('\n') + "\n";
            }
            if (inside) content.Append(line).Append('\n');
        }
        return null; // an unterminated fence declares nothing
    }
}
