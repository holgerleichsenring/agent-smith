using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// p0380: resolves WHICH decision YAML a logged decision belongs to —
/// decisions/&lt;phase&gt;.yaml when the source label is a phase id, else
/// decisions/&lt;runId&gt;.yaml for the active run (the revived `run:` slot of
/// decision.schema.json, parallel to phase decisions). No phase label and no
/// run scope =&gt; null (nothing schema-conformant to write).
/// </summary>
internal sealed partial record DecisionFileLabel(string FileName, string HeaderKey, string Id)
{
    public static DecisionFileLabel? Resolve(string? sourceLabel, string? runId)
    {
        if (sourceLabel is not null && PhasePattern().IsMatch(sourceLabel))
            return new DecisionFileLabel($"{sourceLabel}.yaml", "phase", sourceLabel);
        if (!string.IsNullOrEmpty(runId))
            return new DecisionFileLabel($"{runId}.yaml", "run", runId);
        return null;
    }

    public string Header => $"{HeaderKey}: {Id}\ndecisions:\n";

    // Mirrors the `phase` pattern in .agentsmith/decision.schema.json.
    [GeneratedRegex("^p[0-9a-z]+(?:-[a-z][a-z0-9-]*)?$")]
    private static partial Regex PhasePattern();
}
