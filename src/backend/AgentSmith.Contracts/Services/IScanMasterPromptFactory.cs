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

    /// <summary>p0279: the one-shot coverage nudge re-prompt when the master read too
    /// little source — push a full-surface inventory + per-area review (responsibility
    /// language, read-only), re-emitting the COMPLETE observation array.</summary>
    string BuildCoverageNudge(string originalUserPrompt);
}
