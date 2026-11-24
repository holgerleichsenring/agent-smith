namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: a file a secret provider rendered — what the GitOps commit writes into the PoC's folder.
/// Ciphertext only: nothing in it opens without the target's private key.</summary>
public sealed record PocSecretFile(string FileName, string Content);
