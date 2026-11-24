namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81eb: what a render reads of the revision a desired state names — its port, the source commit
/// its image is tagged with and the digest the registry confirmed.</summary>
public sealed record PocRenderRevision(int Number, int? Port, string? CommitSha, string? Digest);
