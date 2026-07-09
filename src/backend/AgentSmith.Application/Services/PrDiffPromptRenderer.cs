using System.Text;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Turns a PrDiffAnalysis into a prompt-friendly text block for the pr-review
/// skill rounds. Every added/context line carries its NEW-file line number so
/// skills can anchor observations with an exact <c>line_range</c>; removed
/// lines carry no number (they don't exist in the head revision).
/// </summary>
public static class PrDiffPromptRenderer
{
    private const int MaxRenderedLines = 4000;

    public static string Render(PrDiffAnalysis diff)
    {
        var sb = new StringBuilder();
        var rendered = 0;
        foreach (var file in diff.Files)
        {
            sb.AppendLine($"### {file.Path} ({file.Kind.ToString().ToLowerInvariant()})");
            if (file.IsBinary) { sb.AppendLine("(binary file — content not shown)"); sb.AppendLine(); continue; }
            foreach (var hunk in file.Hunks)
            {
                if (rendered >= MaxRenderedLines)
                {
                    sb.AppendLine($"[diff truncated after {MaxRenderedLines} lines — remaining hunks omitted]");
                    return sb.ToString().TrimEnd();
                }
                rendered += RenderHunk(sb, hunk);
            }
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static int RenderHunk(StringBuilder sb, PrHunk hunk)
    {
        sb.AppendLine($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");
        foreach (var line in hunk.Lines)
            sb.AppendLine(RenderLine(line));
        return hunk.Lines.Count + 1;
    }

    private static string RenderLine(PrDiffLine line) => line.Kind switch
    {
        PrDiffLineKind.Added => $"+ {line.NewLineNumber,5}: {line.Content}",
        PrDiffLineKind.Removed => $"-      : {line.Content}",
        _ => $"  {line.NewLineNumber,5}: {line.Content}",
    };
}
