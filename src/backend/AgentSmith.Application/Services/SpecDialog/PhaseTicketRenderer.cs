using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315c: composes a phase ticket from a schema-valid PhaseDraft. The body leads with a
/// human-first markdown summary (goal / why / scope, read from the draft's own yaml); the ticket
/// carries the `phase` label.
/// <para>
/// 2026-09-17-0e79a: a filed phase carries NO fenced spec. The approved set is stored under the
/// ticket's spec key and carried to the run, and a fence in the body would be a second truth —
/// one that anyone with tracker access can edit, and one the source precedence would take. The
/// body states what is wanted and points at the conversation the set was approved in.
/// </para>
/// </summary>
public sealed class PhaseTicketRenderer
{
    public const string PhaseLabel = "phase";

    /// <summary>
    /// 2026-09-13-a3f1: a RECORD, not work. It was filed with the phase label, which hard-binds
    /// routing, so a run started on the summary of a cut and died at the spec gate. Framework-owned
    /// and deliberately not "epic": an operator's own epic label must keep meaning what it means
    /// to them.
    /// <para>
    /// 2026-09-22-b3d7: NOTHING THE FRAMEWORK FILES CARRIES THIS ANY MORE — an approved cut is one
    /// work ticket and no records at all. The constant and its reader stay for the tickets that
    /// ALREADY carry it, and there are two generations of them: the epic PARENT SUMMARIES filed
    /// before 2026-09-17-0e79d widened the label's meaning, and the SLICE RECORDS filed from then
    /// until this phase. Neither is deleted by this phase, both sit on a live board, and each is
    /// refused by the incoming path on this label alone — so dropping the constant, renaming its
    /// value or removing the reader would make every one of them an ordinary ticket overnight,
    /// routable by tag, by area path or by repository, and then run.
    /// </para>
    /// </summary>
    public const string EpicLabel = "phase-epic";

    /// <summary>
    /// A filed phase: the requirement, plus the link back to the approved specification the run
    /// works from. The spec itself is not in the body — it is the approved record.
    /// </summary>
    /// <param name="conversation">The design conversation the set was approved in.</param>
    /// <param name="labelNote">2026-09-18-d518: what the filer's labels bind.</param>
    public PhaseTicketContent RenderPhase(
        PhaseDraft draft, string? conversation = null, string? labelNote = null) =>
        new(Title(draft), PhaseTicketBody.Requirement(
            draft, sb => AppendSpecification(sb, conversation), labelNote));

    /// <summary>The heading the specification pointer is filed under.</summary>
    public const string SpecificationHeading = "## Specification";

    /// <summary>
    /// The pointer every WORK ticket carries: the body is not the spec, the approved record is,
    /// and this is where the run reads it from and where a change to it is made.
    /// </summary>
    private static void AppendSpecification(StringBuilder sb, string? conversation)
    {
        sb.AppendLine(SpecificationHeading);
        sb.AppendLine(
            "The approved phase specification is what the run works from; it is published to "
            + "the ticket branch under `" + Contracts.Specs.SpecSetKey.Root + "/`."
            + (string.IsNullOrWhiteSpace(conversation)
                ? string.Empty
                : $" Approved in design conversation `{conversation}`, which is where a change to it is made."));
        sb.AppendLine();
    }

    /// <summary>
    /// The epic's WORK ticket (2026-09-17-0e79d): the requirement the whole cut answers, listing
    /// its slices in order. One run works every slice from the set stored under this ticket's key,
    /// so the "## Slices" section is what a reader opens the ticket for, not a routing instruction.
    /// <para>
    /// It carries the same specification pointer a single filed phase does: this is the one ticket
    /// a run works from, and a reader who asks where its specification lives must not have to
    /// know that an epic keeps it somewhere else.
    /// </para>
    /// </summary>
    /// <param name="templates">
    /// 2026-09-13-ed5a: the templates the analysis had open when it made this cut, and the
    /// revision each stood at.
    /// </param>
    /// <param name="conversation">The design conversation the set was approved in.</param>
    /// <param name="labelNote">2026-09-18-d518: what the filer's labels bind.</param>
    public PhaseTicketContent RenderEpicParent(
        PhaseDraft parent, IReadOnlyList<PhaseDraft> children,
        IReadOnlyList<TemplateProvenance>? templates = null, string? conversation = null,
        string? labelNote = null) =>
        new(Title(parent), PhaseTicketBody.Requirement(parent, sb =>
        {
            sb.AppendLine("## Slices");
            foreach (var child in children)
                sb.AppendLine($"- `{child.PhaseId}` {child.Goal}{FormatRequires(child)}");
            sb.AppendLine();
            TemplateProvenanceLines.Append(sb, templates);
            AppendSpecification(sb, conversation);
        }, labelNote));

    // 2026-09-17-042eb: the goal is whole in the body; the title only has to be accepted.
    private static string Title(PhaseDraft draft) => TicketTitle.Fit($"{draft.PhaseId}: {draft.Goal}");

    private static string FormatRequires(PhaseDraft draft) =>
        draft.Requires.Count == 0 ? string.Empty : $" (requires: {string.Join(", ", draft.Requires)})";
}
