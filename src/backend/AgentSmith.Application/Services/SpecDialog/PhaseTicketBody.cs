using System.Text;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

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
    /// </summary>
    public static string Requirement(
        PhaseDraft draft, Action<StringBuilder> extraSections, string? labelNote = null) =>
        Done(Shared(draft, ScopeLines, (body, map) =>
        {
            AppendLines(body, AcceptanceCriteriaSection.Heading, DoneLines(map));
            AppendLines(body, "## Preconditions", draft.Requires);
        }, sb => { extraSections(sb); sb.Append(labelNote); }));

    private static IEnumerable<string> DoneLines(IReadOnlyDictionary<string, object?> map) =>
        (OutcomeYamlReader.GetList(map, "done") ?? []).Select(line => CriterionLine.Collapse(line?.ToString() ?? string.Empty));

    private static StringBuilder Shared(
        PhaseDraft draft,
        Func<IReadOnlyDictionary<string, object?>, IEnumerable<string>> middle,
        Action<StringBuilder, IReadOnlyDictionary<string, object?>> tail,
        Action<StringBuilder> extraSections)
    {
        var map = OutcomeYamlReader.ReadMap(draft.Yaml);
        var sb = new StringBuilder();
        sb.AppendLine("## Goal");
        sb.AppendLine(draft.Goal);
        sb.AppendLine();
        AppendLines(sb, "## Why", Decisions(map));
        AppendLines(sb, "## Scope", middle(map));
        tail(sb, map);
        extraSections(sb);
        return sb;
    }

    private static string Done(StringBuilder sb) => sb.ToString().TrimEnd() + "\n";

    private static void AppendLines(StringBuilder sb, string heading, IEnumerable<string> items)
    {
        var list = items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        if (list.Count == 0) return;
        sb.AppendLine(heading);
        foreach (var item in list) sb.AppendLine($"- {item}");
        sb.AppendLine();
    }

    /// <summary>
    /// 2026-09-13-b7ba: a decision is a bare line in the oldest phases and a {key: '…'} map
    /// in every modern one — which used to render as an empty string and be filtered away,
    /// so the REASONING was absent from every ticket this product has ever filed.
    /// </summary>
    private static IEnumerable<string> Decisions(IReadOnlyDictionary<string, object?> map) =>
        (OutcomeYamlReader.GetList(map, "decisions") ?? []).Select(Decision);

    private static string Decision(object? decision) => decision switch
    {
        string line => line,
        Dictionary<object, object?> entry =>
            string.Join(" ", entry.Values.Select(v => v?.ToString()?.Trim()).Where(v => !string.IsNullOrEmpty(v))),
        _ => decision?.ToString() ?? string.Empty,
    };

    /// <summary>The scope as the spec states it — what is in, and what is deliberately out.</summary>
    private static IEnumerable<string> ScopeLines(IReadOnlyDictionary<string, object?> map)
    {
        var scope = OutcomeYamlReader.GetMap(map, "scope");
        if (scope is null) yield break;
        if (OutcomeYamlReader.GetString(scope, "in") is { } inScope && inScope.Trim().Length > 0)
            yield return $"In: {inScope.Trim()}";
        if (OutcomeYamlReader.GetString(scope, "out") is { } outScope && outScope.Trim().Length > 0)
            yield return $"Out: {outScope.Trim()}";
    }

}
