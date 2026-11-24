using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: everything a PoC's sealed files are made from — the provider, the certificate's
/// fingerprint, the namespace, every secret variable, and for pull_mode pull_secret the registry, user and pull token.
/// Set times and key ids are not inputs, so a re-wrap leaves the fingerprint as it was.</summary>
public sealed record PocSealInputs(
    string Provider, string CertificateFingerprint, string Namespace, IReadOnlyDictionary<string, SecretInput> Secrets,
    string PullMode, string? LoginServer = null, string? PullUser = null, SecretInput? PullToken = null);
