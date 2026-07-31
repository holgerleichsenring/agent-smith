using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: renders the CURRENT revision for the planner and the master. It is
/// ADDITIONAL context — the ticket stays pinned verbatim (p0357) and the ledger
/// still seeds from the plan. Empty when the run derived nothing; a master body
/// without the placeholder simply never renders it, so old skill pins keep working.
/// </summary>
public static class WorkSpecPromptSection
{
    public static string Build(PipelineContext pipeline) =>
        pipeline.TryGet<WorkSpecArtifact>(ContextKeys.WorkSpec, out var artifact) && artifact is not null
            ? Build(artifact)
            : string.Empty;

    public static string Build(WorkSpecArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var spec = artifact.Spec;
        var key = new WorkSpecKey(spec.Key);
        var sb = new StringBuilder();
        sb.AppendLine($"## Work spec — revision {spec.Current.Number} ({spec.Current.Cause})");
        sb.AppendLine(
            "The versioned statement of WHAT this work must achieve, derived from the ticket "
            + "and reviewable on the branch. It carries no steps: the plan owns those. The "
            + "ticket above stays authoritative for anything the spec does not cover.");
        sb.AppendLine($"Files: `{key.SpecPath}`, samples in `{key.SamplesPath}`.");
        sb.AppendLine();
        sb.AppendLine($"**Goal:** {spec.Goal}");
        AppendList(sb, "Requirements", spec.Requirements);
        AppendConstraints(sb, artifact);
        AppendList(sb, "Assumptions made", spec.Assumptions);
        return sb.ToString();
    }

    private static void AppendConstraints(StringBuilder sb, WorkSpecArtifact artifact)
    {
        if (artifact.Spec.Constraints.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("### Constraints (carried verbatim from the ticket — do not paraphrase)");
        foreach (var constraint in artifact.Spec.Constraints)
            sb.AppendLine(constraint.SampleAnchor is null
                ? $"- {constraint.Rule}"
                : $"- {constraint.Rule} (sample: `{WorkSpecSampleAnchors.HeadingPrefix}{constraint.SampleAnchor}` in spec.md)");
        if (!string.IsNullOrWhiteSpace(artifact.SamplesMarkdown))
        {
            sb.AppendLine();
            sb.AppendLine("### Samples (spec.md)");
            sb.AppendLine(artifact.SamplesMarkdown);
        }
    }

    private static void AppendList(StringBuilder sb, string heading, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine($"### {heading}");
        foreach (var entry in entries) sb.AppendLine($"- {entry}");
    }
}
