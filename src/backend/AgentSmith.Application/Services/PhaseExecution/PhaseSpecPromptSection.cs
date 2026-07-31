using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0393: renders the validated phase spec for the PLANNER.
///
/// p0315d handed the spec's steps straight through as the approved plan, which held
/// only because its tickets were authored by the operator in-thread — the planning had
/// already happened, in a conversation. A spec states WHAT must become true; with no
/// code samples in it there is no plan inside it. So the spec becomes the planner's
/// input and GeneratePlan derives the HOW against the actual codebase, which is the
/// step a developer performs before writing a line.
///
/// The spec is rendered as its own yaml rather than reformatted: it was schema-validated
/// at the gate, and a prose restatement of a validated artifact is a second version of it
/// that can disagree with the first.
///
/// Empty when the run carries no phase spec, so callers interpolate unconditionally.
/// </summary>
public static class PhaseSpecPromptSection
{
    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) && draft is not null
            ? Build(draft)
            : string.Empty;
    }

    public static string Build(PhaseDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var sb = new StringBuilder();
        sb.AppendLine($"## Phase spec {draft.PhaseId}");
        sb.AppendLine(
            "The validated statement of WHAT this phase must achieve. Plan against it and "
            + "against the repository as it actually is: the spec says what must become true, "
            + "not where in this codebase it happens. Your plan owns the target files, the "
            + "order, and the reproduction or investigation steps the work needs first.");
        sb.AppendLine();
        sb.AppendLine($"**Goal:** {draft.Goal}");
        sb.AppendLine();
        sb.AppendLine("```yaml");
        sb.AppendLine(draft.Yaml.TrimEnd());
        sb.AppendLine("```");
        return sb.ToString();
    }
}
