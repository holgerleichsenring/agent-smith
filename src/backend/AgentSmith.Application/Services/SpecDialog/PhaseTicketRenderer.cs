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
    /// </summary>
    public PhaseTicketContent RenderChildRequirement(PhaseDraft draft, string parentReference) =>
        new(Title(draft), PhaseTicketBody.Requirement(draft, sb =>
        {
            sb.AppendLine($"Parent: {parentReference}");
            sb.AppendLine();
        }));

    /// <summary>The epic parent: the record of a cut, listing its slices in order.</summary>
    public PhaseTicketContent RenderEpicParent(PhaseDraft parent, IReadOnlyList<PhaseDraft> children) =>
        new(Title(parent), PhaseTicketBody.Requirement(parent, sb =>
        {
            sb.AppendLine("## Slices");
            foreach (var child in children)
                sb.AppendLine($"- `{child.PhaseId}` {child.Goal}{FormatRequires(child)}");
            sb.AppendLine();
        }));

    private static string Title(PhaseDraft draft) => $"{draft.PhaseId}: {draft.Goal}";

    private static string FormatRequires(PhaseDraft draft) =>
        draft.Requires.Count == 0 ? string.Empty : $" (requires: {string.Join(", ", draft.Requires)})";
}
