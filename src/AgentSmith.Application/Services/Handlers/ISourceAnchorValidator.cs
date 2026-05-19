using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Enforces the source-anchor contract on <see cref="SkillObservation"/>s
/// whose evidence mode is <see cref="EvidenceMode.AnalyzedFromSource"/>.
///
/// Originally (p0151b) this was a binary drop-or-keep gate, but operators
/// observed valuable findings being silently lost when an LLM emitted
/// <c>analyzed_from_source</c> with no file (or with a file outside the
/// per-call ReadSet from p0151a). The signal in the description was often
/// real even when the label was wrong — losing the entire observation was
/// the wrong response.
///
/// The contract is therefore now: pass observations through unchanged when
/// the anchor holds; <b>downgrade</b> mislabeled ones to
/// <see cref="EvidenceMode.Potential"/> (clearing the unverified
/// <c>File</c> claim) when it does not. Downgrades are logged at Warning
/// so the operator still sees the prompt drift.
/// </summary>
public interface ISourceAnchorValidator
{
    /// <summary>
    /// Returns the observation unchanged if its source anchor holds, or a
    /// downgraded copy (evidence_mode → potential, file cleared) when the
    /// <c>analyzed_from_source</c> claim cannot be verified against the
    /// per-call ReadSet. Never returns null — the observation is always
    /// kept in some form.
    /// </summary>
    SkillObservation EnforceAnchor(
        SkillObservation observation,
        IReadOnlyCollection<string>? readPaths,
        string role,
        ILogger? logger);
}
