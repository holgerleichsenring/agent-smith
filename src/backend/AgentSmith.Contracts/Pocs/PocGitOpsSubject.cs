namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81eb: the Poc row as the GitOps commit reads it — its names, its desired state (Revision and
/// ImageDigest null until the first commit), the seal fingerprint the branch holds and the plain variables.</summary>
public sealed record PocGitOpsSubject(
    long PocId, string Name, string Project, string Target, int Replicas, int? Revision, string? ImageDigest,
    string? ReadinessPath, string? SealFingerprint, string? PlainVariablesJson)
{
    public IReadOnlyList<PocPlainVariable> Plain => PocPlainVariables.Parse(PlainVariablesJson);
}
