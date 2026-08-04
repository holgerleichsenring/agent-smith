using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: extracts the display/linking fields (phase id, goal, requires)
/// from a SCHEMA-VALID phase-spec yaml document into a PhaseDraft. The
/// caller validates first; a document that slips through without the
/// required fields fails loudly here.
/// </summary>
public sealed class PhaseDraftReader
{
    public PhaseDraft Read(string yaml)
    {
        var map = OutcomeYamlReader.ReadMap(yaml);
        var phaseId = OutcomeYamlReader.GetString(map, "phase")
            ?? throw new InvalidOperationException("Schema-valid phase draft has no 'phase' field.");
        var goal = OutcomeYamlReader.GetString(map, "goal")
            ?? throw new InvalidOperationException("Schema-valid phase draft has no 'goal' field.");
        return new PhaseDraft(phaseId, goal, yaml.Trim(), ReadRequires(map))
        {
            // p0393a: the done-list is the run's acceptance contract, so it is read
            // here rather than re-parsed by every consumer of the draft.
            Done = ReadStrings(map, "done"),
            // p0394a: the spec's steps are the run's plan of record — they seed the
            // progress ledger and render as the master's plan section.
            Steps = ReadSteps(map),
        };
    }

    // The schema requires an id per step and allows action as a single line or an
    // array of lines; the optional `path` is the step's target hint, passed through
    // verbatim (repo-qualified exactly as the spec wrote it).
    private static IReadOnlyList<PhaseStep> ReadSteps(IReadOnlyDictionary<string, object?> map)
    {
        if (!map.TryGetValue("steps", out var value) || value is not List<object?> list) return [];
        var steps = new List<PhaseStep>();
        foreach (var entry in list)
        {
            var step = Read(entry, fallbackId: (steps.Count + 1).ToString());
            if (step is not null) steps.Add(step);
        }
        return steps;
    }

    private static PhaseStep? Read(object? entry, string fallbackId)
    {
        if (entry is not Dictionary<object, object?> step)
        {
            var line = entry?.ToString();
            return string.IsNullOrWhiteSpace(line) ? null : new PhaseStep(fallbackId, line, null);
        }
        var id = GetStepString(step, "id");
        var action = ReadAction(step);
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(action)) return null;
        return new PhaseStep(
            string.IsNullOrWhiteSpace(id) ? fallbackId : id!,
            string.IsNullOrWhiteSpace(action) ? id! : action!,
            GetStepString(step, "path"));
    }

    private static string? ReadAction(Dictionary<object, object?> step)
    {
        if (!step.TryGetValue("action", out var value) || value is null) return null;
        return value switch
        {
            string single => single,
            List<object?> lines => string.Join(" ", lines
                .Select(l => l?.ToString())
                .Where(l => !string.IsNullOrWhiteSpace(l))),
            _ => value.ToString(),
        };
    }

    private static string? GetStepString(Dictionary<object, object?> step, string key) =>
        step.TryGetValue(key, out var value) && value is string text
        && !string.IsNullOrWhiteSpace(text) ? text : null;

    // The schema allows requires as a single string or an array of strings.
    private static IReadOnlyList<string> ReadRequires(IReadOnlyDictionary<string, object?> map) =>
        ReadStrings(map, "requires");

    private static IReadOnlyList<string> ReadStrings(
        IReadOnlyDictionary<string, object?> map, string field)
    {
        if (!map.TryGetValue(field, out var value) || value is null) return [];
        return value switch
        {
            string single => [single],
            List<object?> list => [.. list.Select(e => e?.ToString() ?? string.Empty)],
            _ => [],
        };
    }
}
