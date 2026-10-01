using System.Text;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-b7ba: the body shape a filed ticket takes. A REQUIREMENT states what is wanted
/// and why and carries no fenced block at all.
/// <para>
/// 2026-09-17-0e79a: the WORK ORDER shape is gone with its last caller. It ended in one fenced
/// yaml block, which the source precedence would take as the spec — a second truth beside the
/// approved record, editable by anyone with tracker access. The extractor still inverts that
/// shape for HAND-WRITTEN phase tickets, and the p0315d contract note lives there now.
/// </para>
/// </summary>
internal static class PhaseTicketBody
{
    /// <summary>
    /// What is wanted and why — and deliberately NOT the steps. Carrying the cut in prose
    /// would let the deriver anchor on it and reproduce the cut it was supposed to redo.
    /// <para>
    /// 2026-09-17-042eb: the done list travels as acceptance criteria — an outcome, not a cut —
    /// one line per criterion, and every requires: edge is a precondition.
    /// </para>
    /// <para>
    /// 2026-09-22-b3d7: no sibling filter any more. It existed for the slice records, which were
    /// the one rendering that had siblings to subtract; the two renderings that survive both
    /// passed an empty set, so the filter could only ever remove nothing.
    /// </para>
    /// <para>
    /// 2026-09-18-d518: the label note, when the filer passes one, is LAST in the body — after
    /// everything the ticket is about, wrapped in its own marker pair.
    /// </para>
    /// <para>
    /// 2026-09-25-8e51e: the WHOLE rendering is wrapped in <see cref="FramedTicketRegion"/>'s
    /// pair, so an amendment replaces exactly what the framework wrote and leaves a person's own
    /// prose — above it, below it, anywhere outside the markers — untouched. The note keeps its
    /// own inner pair: the stripper that removes it at the ticket-fetch door matches that one,
    /// and the region says nothing about which parts of itself a model may read.
    /// </para>
    /// <para>
    /// 2026-10-01-f5c3b: the READER'S shape — the goal leads unheaded and verbatim (divergence
    /// finds it word for word), then Why, What changes, Acceptance criteria, Out of scope and
    /// Preconditions. The criteria heading stays: two readers find criteria by it.
    /// </para>
    /// </summary>
    public static string Requirement(
        PhaseDraft draft, Action<StringBuilder> extraSections, string? labelNote = null)
    {
        var map = OutcomeYamlReader.ReadMap(draft.Yaml);
        var sb = new StringBuilder();
        sb.AppendLine(draft.Goal);
        sb.AppendLine();
        AppendItems(sb, "## Why", PhaseSpecProse.Decisions(map));
        AppendProse(sb, "## What changes", PhaseSpecProse.ScopeIn(map));
        AppendItems(sb, AcceptanceCriteriaSection.Heading, DoneLines(draft));
        AppendProse(sb, "## Out of scope", PhaseSpecProse.ScopeOut(map));
        AppendItems(sb, "## Preconditions", draft.Requires);
        extraSections(sb);
        sb.Append(labelNote);
        return FramedTicketRegion.Wrap(sb.ToString().TrimEnd() + "\n");
    }

    // 2026-10-01-f5c3a: the draft's done list, never the raw yaml — a scenario entry is a
    // mapping there and would print as its type name.
    private static IEnumerable<string> DoneLines(PhaseDraft draft) =>
        draft.Done.Select(CriterionLine.Collapse);

    private static void AppendItems(StringBuilder sb, string heading, IEnumerable<string> items)
    {
        var list = items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        if (list.Count == 0) return;
        sb.AppendLine(heading);
        foreach (var item in list) sb.AppendLine($"- {item}");
        sb.AppendLine();
    }

    private static void AppendProse(StringBuilder sb, string heading, string text)
    {
        if (text.Length == 0) return;
        sb.AppendLine(heading);
        sb.AppendLine(text);
        sb.AppendLine();
    }
}
