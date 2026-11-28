using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// p0380: resolves WHICH decision YAML a logged decision belongs to — decisions/&lt;spec&gt;.yaml.
/// 2026-11-28-db88d: the spec is the source label when it is a spec id, else the spec the logging step works
/// (IRunContextAccessor.CurrentSpecId), so several runs of one spec append to one file. A decision with neither
/// names no file: it is recorded on the run's event stream only, never in a file named after a run. Files an
/// earlier version named after a run (the schema's `run:` slot) stay as they are.
/// </summary>
internal sealed partial record DecisionFileLabel(string FileName, string HeaderKey, string Id)
{
    public static DecisionFileLabel? Resolve(string? sourceLabel, string? specId)
    {
        if (sourceLabel is not null && SpecPattern().IsMatch(sourceLabel))
            return new DecisionFileLabel($"{sourceLabel}.yaml", "spec", sourceLabel);
        if (specId is not null && SpecPattern().IsMatch(specId))
            return new DecisionFileLabel($"{specId}.yaml", "spec", specId);
        return null;
    }

    public string Header => $"{HeaderKey}: {Id}\ndecisions:\n";

    // Mirrors the `spec` pattern in .agentsmith/decision.schema.json: the closed
    // counter namespace, or a p0507 date-minted id. The date branch cannot collide with
    // a run id — a run id carries a `T` and a time where this one carries the suffix.
    [GeneratedRegex(@"^(?:p\d{4,6}[a-z]?|\d{4}-\d{2}-\d{2}-[0-9a-f]{4}[a-z]?)(?:-[a-z][a-z0-9-]*)?$")]
    private static partial Regex SpecPattern();
}
