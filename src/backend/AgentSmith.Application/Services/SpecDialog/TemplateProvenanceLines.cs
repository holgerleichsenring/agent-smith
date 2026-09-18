using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-ed5a: the templates the analysis had open while it made a cut, and the revision
/// each stood at, as plain body lines. A reader who asks why the slices are shaped this way
/// must find the answer on the ticket, without opening a run.
/// <para>
/// Its own type because a REQUIREMENT body opens no fence (2026-09-13-b7ba) and the wording of
/// "read at" versus "declared, unread" is a claim in its own right — the renderer that appends
/// it has no other reason to hold it.
/// </para>
/// </summary>
internal static class TemplateProvenanceLines
{
    public static void Append(StringBuilder sb, IReadOnlyList<TemplateProvenance>? templates)
    {
        ArgumentNullException.ThrowIfNull(sb);
        if (templates is null || templates.Count == 0) return;
        sb.AppendLine("## Templates this cut was made against");
        foreach (var template in templates)
            sb.AppendLine($"- `{template.Address}` — {template.Repo} {Revision(template)}");
        sb.AppendLine();
    }

    // "Read at" is a measurement and only the opened scope has one; a declaration is what
    // the other kind carries, and saying which is what keeps the body honest.
    private static string Revision(TemplateProvenance template) =>
        template.Revision.Length == 0
            ? (template.Opened ? "read at its own default revision" : "declared with no revision, unread")
            : template.Opened ? $"read at `{template.Revision}`" : $"declared at `{template.Revision}`, unread";
}
