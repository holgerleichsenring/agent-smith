using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Matches Nuclei / ZAP findings to mapped route handlers from the
/// ApiCodeContext. Deterministic — URL+method against route templates.
/// Findings without a matching route emit a correlation row with
/// Handler=null so callers can reason about coverage.
/// </summary>
public interface IFindingHandlerCorrelator
{
    IReadOnlyList<FindingHandlerCorrelation> Correlate(
        NucleiResult? nuclei,
        ZapResult? zap,
        ApiCodeContext? codeContext);
}
