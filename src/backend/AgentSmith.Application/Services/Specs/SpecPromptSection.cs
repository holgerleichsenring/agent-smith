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

        AppendMocks(sb, phase.MockPaths ?? []);
        return sb.ToString();
    }

    // 2026-10-01-283dh: a mock beside the phase's spec is what this phase's screen should look like.
    private static void AppendMocks(StringBuilder sb, IReadOnlyList<string> mocks)
    {
        if (mocks.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("### This phase's design mock");
        sb.AppendLine(
            "A reviewer placed the HTML mock(s) below beside this phase's spec, in the repository that "
            + "carries the spec set. It shows what this phase's screen should look like. Read its CSS with "
            + "read_file for the exact values; when render_reference is on your surface, pass the path as "
            + "its source to see the mock and read its computed styles. It is a reviewer's file: never edit or delete it.");
        foreach (var mock in mocks) sb.AppendLine($"- `{mock}`");
    }
}
