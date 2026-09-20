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
    /// 2026-09-17-0e79d: the VALUE stays; its meaning widens from "the record of a cut" to "a
    /// record, not work" — an approved epic files one work ticket and one record per slice, and the
    /// records carry this. Renaming it would make every parent already filed routable overnight.
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
            draft, new HashSet<string>(), sb => AppendSpecification(sb, conversation), labelNote));

    /// <summary>The heading the specification pointer is filed under.</summary>
    public const string SpecificationHeading = "## Specification";

    /// <summary>
    /// The pointer every WORK ticket carries: the body is not the spec, the approved record is,
    /// and this is where the run reads it from and where a change to it is made. A slice record
    /// gets none — no run works one, so nothing is ever published to a branch of its own.
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
    /// 2026-09-13-b7ba: an epic CHILD is a requirement, not a work order. It is filed today
    /// and worked in three weeks, after its siblings have moved the code — and an embedded
    /// spec would win over the repository it claims to plan against, because SpecSourceResolver
    /// takes a spec in the description and never calls the deriver at all.
    /// <para>
    /// 2026-09-17-042ea: no Parent line. The tracker links the child to its parent and the label
    /// stamps it; a line in the body was a segment the deriver had to carry or discard.
    /// </para>
    /// <para>
    /// 2026-09-17-0e79d: this is the SLICE RECORD's body — the record label, no stamps, no spec,
    /// read by a person and not by a deriver: the run works the set stored under the work ticket.
    /// </para>
    /// </summary>
    /// <param name="siblingIds">The phase ids of the epic's slices; only these leave the body.</param>
    public PhaseTicketContent RenderChildRequirement(PhaseDraft draft, IReadOnlySet<string> siblingIds) =>
        new(Title(draft), PhaseTicketBody.Requirement(draft, siblingIds, _ => { }));

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
        new(Title(parent), PhaseTicketBody.Requirement(parent, new HashSet<string>(), sb =>
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
