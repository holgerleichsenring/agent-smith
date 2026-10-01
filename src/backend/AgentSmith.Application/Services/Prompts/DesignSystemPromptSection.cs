using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-01-283dg: the design system of each repository that carries a root DESIGN.md,
/// appended to the master body beside the template section — a server-built section reaches
/// every pinned master without a skills release. The token frontmatter is VERBATIM: the tokens
/// are the exact half of a design system and a summary would reintroduce an estimate. The
/// documents together are capped at <see cref="Budget"/> characters, cut from the end so the
/// prose goes before the frontmatter; every cut is named with the path the rest is read from.
/// Empty when no repository carries one.
/// </summary>
public static class DesignSystemPromptSection
{
    public const int Budget = 32_000;

    public static string Render(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<ContextDocument>>(ContextKeys.DesignSystem, out var documents)
            ? Render(documents)
            : string.Empty;

    public static string Render(IReadOnlyList<ContextDocument>? documents)
    {
        if (documents is null || documents.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        var left = Budget;
        foreach (var document in documents)
        {
            var address = $"{document.SandboxKey}/{ProjectMetaPaths.DesignSystem}";
            sb.Append($"\n\n## Design system — {document.SandboxKey}\n")
              .Append($"`{address}`: its frontmatter holds the design tokens as exact values — use them ")
              .Append("as written; the prose after it says how they are meant.\n\n");
            var (shown, cut) = Fit(document.Content, left);
            sb.Append(shown.TrimEnd());
            if (cut > 0)
                sb.Append($"\n\n[{address} continues for {cut} more characters, cut at the "
                          + $"{Budget}-character design-system budget — read it with read_file \"{address}\".]");
            left -= shown.Length;
        }
        return sb.ToString();
    }

    // Cut from the END, at a line boundary: the frontmatter opens the file, so the prose goes
    // first and the tokens stay whole whenever they fit in what the budget leaves.
    private static (string Shown, int Cut) Fit(string content, int left)
    {
        if (content.Length <= left) return (content, 0);
        if (left <= 0) return (string.Empty, content.Length);
        var lineEnd = content.LastIndexOf('\n', left - 1);
        var keep = lineEnd > 0 ? lineEnd + 1 : left;
        return (content[..keep], content.Length - keep);
    }
}
