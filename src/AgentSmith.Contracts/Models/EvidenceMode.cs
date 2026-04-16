namespace AgentSmith.Contracts.Models;

/// <summary>
/// Indicates whether a finding was confirmed by an active probe or inferred from static analysis.
/// </summary>
public enum EvidenceMode
{
    /// <summary>
    /// Inferred from schema, static analysis, or pattern matching only.
    /// </summary>
    Potential,

    /// <summary>
    /// Confirmed by an authenticated HTTP probe response.
    /// </summary>
    Confirmed
}
