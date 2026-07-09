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
        return new PhaseDraft(phaseId, goal, yaml.Trim(), ReadRequires(map));
    }

    // The schema allows requires as a single string or an array of strings.
    private static IReadOnlyList<string> ReadRequires(IReadOnlyDictionary<string, object?> map)
    {
        if (!map.TryGetValue("requires", out var value) || value is null) return [];
        return value switch
        {
            string single => [single],
            List<object?> list => [.. list.Select(e => e?.ToString() ?? string.Empty)],
            _ => [],
        };
    }
}
