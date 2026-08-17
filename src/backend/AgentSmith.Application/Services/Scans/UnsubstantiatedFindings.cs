using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a: which delivered findings nobody has actually checked.
/// <para>
/// p0429 had one answer: the ones the master's SILENCE promoted, named by the merge. That
/// is right for a repo scan, where the master read the code and its curation is authorship.
/// </para>
/// <para>
/// An api-scan's master never touches the live system — it reads a scanner's report about
/// it. So a live-target claim it repeats has exactly as much authorship behind it as a raw
/// scanner fact: none. Those are added here, which is what makes the api-scan's findings
/// substantiated at all; its preset has no merge to name them.
/// </para>
/// </summary>
public static class UnsubstantiatedFindings
{
    public static IReadOnlyList<SkillObservation> In(
        PipelineContext pipeline, IReadOnlyList<SkillObservation> delivered)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(delivered);
        var promoted = pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.UnvouchedFindings, out var unvouched) && unvouched is not null
                ? unvouched
                : [];
        return [.. promoted.Concat(delivered.Where(IsLiveTargetClaim)).Distinct()];
    }

    /// <summary>
    /// A claim about the running system rather than about the source: it names an endpoint
    /// or a schema and has no readable file:line behind it.
    /// </summary>
    private static bool IsLiveTargetClaim(SkillObservation finding) =>
        (!string.IsNullOrWhiteSpace(finding.ApiPath) || !string.IsNullOrWhiteSpace(finding.SchemaName))
        && finding is not { EvidenceMode: EvidenceMode.AnalyzedFromSource, StartLine: > 0 };
}
