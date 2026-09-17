using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315c: composes a phase ticket from a schema-valid PhaseDraft. The body
/// leads with a human-first markdown summary (goal / why / scope, read from
/// the draft's own yaml) and ends with exactly ONE fenced ```yaml block
/// holding the spec verbatim; the ticket carries the `phase` label. THIS IS
/// THE p0315d CONTRACT: the phase-execution extractor inverts it by taking
/// the single ```yaml block out of a phase-labelled ticket body — nothing
/// else in the body may open a fenced block.
/// </summary>
public sealed class PhaseTicketRenderer
{
    public const string PhaseLabel = "phase";

    /// <summary>
    /// 2026-09-13-a3f1: what an epic PARENT carries instead. The parent is the record of a
    /// cut, not a unit of work — it was filed with the phase label, which hard-binds routing,
    /// so a run started on the summary of a cut and died at the spec gate. Framework-owned
    /// and deliberately not "epic": an operator's own epic label must keep meaning what it
    /// means to them.
    /// </summary>
    public const string EpicLabel = "phase-epic";

    /// <summary>
    /// A WORK ORDER: one phase, cut against the repository as it is, filed to be worked now.
    /// Ends in the single fenced yaml block the phase-execution extractor inverts.
    /// </summary>
    public PhaseTicketContent RenderPhase(PhaseDraft draft) =>
        new(Title(draft), PhaseTicketBody.WorkOrder(draft, _ => { }));

    /// <summary>
    /// 2026-09-13-b7ba: an epic CHILD is a requirement, not a work order. It is filed today
    /// and worked in three weeks, after its siblings have moved the code — and an embedded
    /// spec would win over the repository it claims to plan against, because SpecSourceResolver
    /// takes a spec in the description and never calls the deriver at all.
    /// <para>
    /// 2026-09-17-042ea: no Parent line. The tracker links the child to its parent and the label
    /// stamps it; a line in the body was a segment the deriver had to carry or discard.
    /// </para>
    /// </summary>
    public PhaseTicketContent RenderChildRequirement(PhaseDraft draft) =>
        new(Title(draft), PhaseTicketBody.Requirement(draft, _ => { }));

    /// <summary>The epic parent: the record of a cut, listing its slices in order.</summary>
    /// <param name="templates">
    /// 2026-09-13-ed5a: the templates the analysis had open when it made this cut, and the
    /// revision each stood at. A reader who asks why the slices are shaped this way must find
    /// the answer on the ticket, without opening a run — and the lines are plain, because a
    /// REQUIREMENT body opens no fence (2026-09-13-b7ba).
    /// </param>
    public PhaseTicketContent RenderEpicParent(
        PhaseDraft parent, IReadOnlyList<PhaseDraft> children,
        IReadOnlyList<TemplateProvenance>? templates = null) =>
        new(Title(parent), PhaseTicketBody.Requirement(parent, sb =>
        {
            sb.AppendLine("## Slices");
            foreach (var child in children)
                sb.AppendLine($"- `{child.PhaseId}` {child.Goal}{FormatRequires(child)}");
            sb.AppendLine();
            AppendTemplates(sb, templates);
        }));

    private static void AppendTemplates(
        StringBuilder sb, IReadOnlyList<TemplateProvenance>? templates)
    {
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

    private static string Title(PhaseDraft draft) => $"{draft.PhaseId}: {draft.Goal}";

    private static string FormatRequires(PhaseDraft draft) =>
        draft.Requires.Count == 0 ? string.Empty : $" (requires: {string.Join(", ", draft.Requires)})";
}
