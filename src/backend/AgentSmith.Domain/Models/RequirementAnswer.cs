namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-3c12: what the scan STATED about one entry of the standard at one station of
/// one entry group, before anything checked it.
/// <para>
/// The model answers the entries it is handed and does not choose them: the station comes
/// from the map the scan stated, the entry from the lens table, and both live outside the
/// model — which is what makes the denominator external. What the model contributes is the
/// verdict and the evidence for it, and the evidence is what the run then settles.
/// </para>
/// </summary>
public sealed record RequirementAnswer(
    string Group,
    VerificationStation Station,
    string RequirementId,
    RequirementOperation Operation,
    RequirementDisposition Disposition,
    RequirementScope Scope,
    string? File,
    int StartLine,
    IReadOnlyList<string> Members,
    string? MissingInput,
    string Note);
