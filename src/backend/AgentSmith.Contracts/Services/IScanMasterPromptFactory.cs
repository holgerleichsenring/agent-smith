using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0278: builds the user prompt for a SCAN/REVIEW master (output_schema == observation).
/// Unlike the coding user prompt (implement + verify build/tests), this carries the
/// scanner findings (and, for api-security, the OpenAPI spec) inline and frames the run
/// as a read-only security review that emits an observation array — never a code change.
/// </summary>
public interface IScanMasterPromptFactory
{
    string Build(PipelineContext pipeline, Repository repository, IReadOnlyList<string> repoNames);

    /// <summary>2026-09-01-0e80: the scanners' raw output, presented in a SECOND turn once
    /// the master has committed to what it found on its own — which facts it already
    /// covered, which it now judges real, which it dismisses. Null when there is nothing to
    /// reconcile separately: an api scan, whose master inputs ARE the scanner reports, or a
    /// repository scan whose scanners found nothing.</summary>
    string? BuildReconciliation(PipelineContext pipeline);

    /// <summary>p0279: the one-shot coverage nudge re-prompt when the master read too
    /// little source — push a full-surface inventory + per-area review (responsibility
    /// language, read-only), re-emitting the COMPLETE observation array.</summary>
    string BuildCoverageNudge(string originalUserPrompt);
}
