using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Rejects <see cref="SkillObservation"/>s whose <see cref="EvidenceMode.AnalyzedFromSource"/>
/// claim is not backed by an actual file read in the current skill call.
/// Implemented per p0151b: the validator reads the call's
/// <c>ReadPaths</c> (populated by <c>LoopTraceCollector.ReadSet</c> in
/// p0151a) and drops observations whose <c>File</c> is missing or not
/// in the set. Observations with other evidence modes pass through —
/// scanner/swagger/design findings are anchored by other fields, not by
/// source reads.
/// </summary>
public interface ISourceAnchorValidator
{
    bool IsAnchored(
        SkillObservation observation,
        IReadOnlyCollection<string>? readPaths,
        string role,
        ILogger? logger);
}
