using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: renders the CURRENT phase's markdown companion — the verbatim ticket
/// spans this phase must honour — plus where the phase sits in the sequence.
/// <para>
/// This is the section that carries the migration manual's code. The phase yaml
/// states WHAT; the companion carries the naming rules, forbidden APIs and
/// templates byte-identical, because a summary of a naming contract is how a
/// migration silently drifts.
/// </para>
/// </summary>
public static class SpecPromptSection
{
    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return string.Empty;
        if (!pipeline.TryGet<Contracts.Models.PhaseDraft>(ContextKeys.PhaseSpec, out var draft)
            || draft is null)
            return string.Empty;
        var phase = set.Phases.FirstOrDefault(p => p.PhaseId == draft.PhaseId);
        return phase is null ? string.Empty : Build(set, phase);
    }

    public static string Build(SpecSet set, SpecPhase phase)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(phase);
        var sb = new StringBuilder();
        var index = set.Phases.ToList().FindIndex(p => p.PhaseId == phase.PhaseId) + 1;
        sb.AppendLine($"## Phase {index} of {set.Phases.Count}: {phase.PhaseId}");
        sb.AppendLine(
            "This run works through an ordered sequence derived from the ticket. You are "
            + "responsible for THIS phase only — the phases after it are separate work with "
            + "their own done-lists, and doing them here is scope you were not given.");
        if (index > 1)
            sb.AppendLine(
                $"Phases 1–{index - 1} already ran on this branch: their changes are in the "
                + "working tree, and they are not yours to revise.");

        if (!string.IsNullOrWhiteSpace(phase.Markdown))
        {
            sb.AppendLine();
            sb.AppendLine("### Carried verbatim from the ticket — never paraphrase these");
            sb.AppendLine(
                "Every block below was cut out of the ticket byte for byte. Where one is a "
                + "naming rule, a forbidden API or a code template, follow it exactly as "
                + "written; a plausible copy of a contract is a broken contract.");
            sb.AppendLine();
            sb.AppendLine(phase.Markdown.TrimEnd());
        }

        return sb.ToString();
    }
}
