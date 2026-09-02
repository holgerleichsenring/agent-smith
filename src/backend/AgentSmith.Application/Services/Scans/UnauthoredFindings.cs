using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a/2026-09-01-85b2: which findings NOBODY authored — the only ones an unresolvable
/// citation is allowed to delete.
/// <para>
/// A master's silence promoted a raw scanner fact to delivery; nobody read it and nobody
/// vouched for it, so a citation resolving against nothing is invention and it goes. An
/// api-scan's master never touches the live system — it reads a scanner's report about it —
/// so a live-target claim it repeats has exactly as much authorship behind it as a raw
/// scanner fact: none.
/// </para>
/// <para>
/// Everything else has an author. The checked set is now every delivered finding, and a
/// master's own finding whose cited path the reader could not open is a reader that could
/// not read, not a master that invented a file. It is delivered unchanged.
/// </para>
/// </summary>
public static class UnauthoredFindings
{
    public static IReadOnlyList<SkillObservation> In(
        PipelineContext pipeline, IReadOnlyList<SkillObservation> findings)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(findings);
        var promoted = pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.UnvouchedFindings, out var unvouched) && unvouched is not null
                ? unvouched
                : [];
        return [.. findings.Where(f => promoted.Contains(f) || IsLiveTargetClaim(f))];
    }

    /// <summary>
    /// A claim about the running system rather than about the source: it names an endpoint
    /// or a schema and has no readable file:line behind it.
    /// </summary>
    private static bool IsLiveTargetClaim(SkillObservation finding) =>
        (!string.IsNullOrWhiteSpace(finding.ApiPath) || !string.IsNullOrWhiteSpace(finding.SchemaName))
        && finding is not { EvidenceMode: EvidenceMode.AnalyzedFromSource, StartLine: > 0 };
}
