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

    /// <summary>Renders one phase ticket; epic children pass their parent's reference.</summary>
    public PhaseTicketContent RenderPhase(PhaseDraft draft, string? parentReference = null) =>
        new(Title(draft), BuildBody(draft, sb =>
        {
            if (parentReference is not null)
            {
                sb.AppendLine($"Parent: {parentReference}");
                sb.AppendLine();
            }
        }));

    /// <summary>Renders the epic parent, listing its slices in order.</summary>
    public PhaseTicketContent RenderEpicParent(PhaseDraft parent, IReadOnlyList<PhaseDraft> children) =>
        new(Title(parent), BuildBody(parent, sb =>
        {
            sb.AppendLine("## Slices");
            foreach (var child in children)
                sb.AppendLine($"- `{child.PhaseId}` {child.Goal}{FormatRequires(child)}");
            sb.AppendLine();
        }));

    private static string Title(PhaseDraft draft) => $"{draft.PhaseId}: {draft.Goal}";

    private static string BuildBody(PhaseDraft draft, Action<StringBuilder> extraSections)
    {
        var map = OutcomeYamlReader.ReadMap(draft.Yaml);
        var sb = new StringBuilder();
        sb.AppendLine("## Goal");
        sb.AppendLine(draft.Goal);
        sb.AppendLine();
        AppendLines(sb, "## Why", Decisions(map));
        AppendLines(sb, "## Scope", StepActions(map));
        AppendLines(sb, "## Requires", draft.Requires);
        extraSections(sb);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("```yaml");
        sb.AppendLine(draft.Yaml.Trim());
        sb.AppendLine("```");
        return sb.ToString().TrimEnd() + "\n";
    }

    private static void AppendLines(StringBuilder sb, string heading, IEnumerable<string> items)
    {
        var list = items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        if (list.Count == 0) return;
        sb.AppendLine(heading);
        foreach (var item in list) sb.AppendLine($"- {item}");
        sb.AppendLine();
    }

    private static IEnumerable<string> Decisions(IReadOnlyDictionary<string, object?> map) =>
        (OutcomeYamlReader.GetList(map, "decisions") ?? []).Select(d => d as string ?? string.Empty);

    private static IEnumerable<string> StepActions(IReadOnlyDictionary<string, object?> map) =>
        (OutcomeYamlReader.GetList(map, "steps") ?? []).Select(StepAction);

    private static string StepAction(object? step)
    {
        if (step is not Dictionary<object, object?> map) return step?.ToString() ?? string.Empty;
        var action = map.TryGetValue("action", out var a) ? a as string : null;
        var id = map.TryGetValue("id", out var i) ? i as string : null;
        return action ?? id ?? string.Empty;
    }

    private static string FormatRequires(PhaseDraft draft) =>
        draft.Requires.Count == 0 ? string.Empty : $" (requires: {string.Join(", ", draft.Requires)})";
}
