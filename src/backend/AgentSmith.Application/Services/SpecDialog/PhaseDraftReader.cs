using AgentSmith.Application.Services.Specs;
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
        var phaseId = OutcomeYamlReader.GetString(map, "spec")
            ?? throw new InvalidOperationException("Schema-valid spec draft has no 'spec' field.");
        var goal = OutcomeYamlReader.GetString(map, "goal")
            ?? throw new InvalidOperationException("Schema-valid phase draft has no 'goal' field.");
        return new PhaseDraft(phaseId, goal, yaml.Trim(), PhaseDraftLists.Strings(map, "requires"))
        {
            // p0393a: the done-list is the run's acceptance contract, so it is read
            // here rather than re-parsed by every consumer of the draft.
            // 2026-10-01-f5c3a: a scenario entry arrives as its one line form.
            Done = DoneCriterion.Lines(map),
            // p0394a: the spec's steps are the run's plan of record — they seed the
            // progress ledger and render as the master's plan section.
            Steps = ReadSteps(map),
            // 2026-09-15-6d9c: the test names, for the pane that shows what would be filed.
            Tests = PhaseDraftLists.Strings(map, "tests"),
            // 2026-09-07-b7e2: what the derivation looked up and what it assumed —
            // absent on every spec written before the derivation could look.
            Facts = PhaseDraftLists.Facts(map),
            // 2026-10-02-3f06a: an assumption written as {claim, check} is read as its claim.
            Assumptions = PhaseDraftLists.Assumptions(map),
            // 2026-09-08-1830: the contexts the phase declares it changes.
            Contexts = PhaseDraftLists.Strings(map, "contexts"),
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
        var id = PhaseDraftLists.GetString(step, "id");
        var action = ReadAction(step);
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(action)) return null;
        return new PhaseStep(
            string.IsNullOrWhiteSpace(id) ? fallbackId : id!,
            string.IsNullOrWhiteSpace(action) ? id! : action!,
            PhaseDraftLists.GetString(step, "path"));
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
}
