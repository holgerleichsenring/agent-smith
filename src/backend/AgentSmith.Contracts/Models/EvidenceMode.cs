namespace AgentSmith.Contracts.Models;

/// <summary>
/// Indicates how a finding was substantiated.
/// </summary>
public enum EvidenceMode
{
    /// <summary>
    /// Inferred from schema or pattern matching only.
    /// </summary>
    Potential,

    /// <summary>
    /// Confirmed by an authenticated HTTP probe response.
    /// </summary>
    Confirmed,

    /// <summary>
    /// Backed by direct source-code analysis. Carries file:line evidence.
    /// </summary>
    AnalyzedFromSource
}
