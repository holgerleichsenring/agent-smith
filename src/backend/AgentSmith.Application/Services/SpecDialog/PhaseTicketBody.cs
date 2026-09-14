using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-b7ba: the two body shapes a filed ticket can take.
/// <para>
/// A WORK ORDER ends in exactly one fenced yaml block holding the spec verbatim — the
/// p0315d contract the phase-execution extractor inverts. A REQUIREMENT states what is
/// wanted and why and carries no block at all, so the run that picks it up derives its
/// phases against the repository as it then is, rather than replaying a cut made weeks
/// earlier against a repository that has since moved.
/// </para>
/// <para>
/// Carved out of the renderer because there are now two shapes and one of them must NOT
/// open a fence — a rule that is easier to keep when the two are written side by side.
/// </para>
/// </summary>
internal static class PhaseTicketBody
{
    public static string WorkOrder(PhaseDraft draft, Action<StringBuilder> extraSections)
    {
        var sb = Shared(draft, StepActions, extraSections);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("```yaml");
        sb.AppendLine(draft.Yaml.Trim());
        sb.AppendLine("```");
        return Done(sb);
    }

    /// <summary>
    /// What is wanted and why — and deliberately NOT the steps. Carrying the cut in prose
    /// would let the deriver anchor on it and reproduce the cut it was supposed to redo.
    /// </summary>
    public static string Requirement(PhaseDraft draft, Action<StringBuilder> extraSections) =>
        Done(Shared(draft, ScopeLines, extraSections));

    private static StringBuilder Shared(
        PhaseDraft draft,
        Func<IReadOnlyDictionary<string, object?>, IEnumerable<string>> middle,
        Action<StringBuilder> extraSections)
    {
        var map = OutcomeYamlReader.ReadMap(draft.Yaml);
        var sb = new StringBuilder();
        sb.AppendLine("## Goal");
        sb.AppendLine(draft.Goal);
        sb.AppendLine();
        AppendLines(sb, "## Why", Decisions(map));
        AppendLines(sb, "## Scope", middle(map));
        AppendLines(sb, "## Requires", draft.Requires);
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

    private static IEnumerable<string> StepActions(IReadOnlyDictionary<string, object?> map) =>
        (OutcomeYamlReader.GetList(map, "steps") ?? []).Select(StepAction);

    private static string StepAction(object? step)
    {
        if (step is not Dictionary<object, object?> map) return step?.ToString() ?? string.Empty;
        var action = map.TryGetValue("action", out var a) ? a as string : null;
        var id = map.TryGetValue("id", out var i) ? i as string : null;
        return action ?? id ?? string.Empty;
    }
}
