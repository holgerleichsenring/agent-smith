namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: what the variable write and the sealing step read of a PoC — its project, name and
/// target, the fingerprint of what the GitOps branch holds sealed, and the plain variables.</summary>
public sealed record PocVariableSubject(
    long PocId, string Name, string Project, string Target, string? SealFingerprint, string? PlainVariablesJson)
{
    public IReadOnlyList<PocPlainVariable> Plain => PocPlainVariables.Parse(PlainVariablesJson);
}
