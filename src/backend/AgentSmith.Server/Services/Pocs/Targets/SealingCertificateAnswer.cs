namespace AgentSmith.Server.Services.Pocs.Targets;

/// <summary>2026-11-23-933dg: a Sealed Secrets certificate as the form shows it — the PEM, its SHA-256 fingerprint and its
/// validity. Public material only.</summary>
internal sealed record SealingCertificateAnswer(string Pem, string Fingerprint, DateTimeOffset NotBefore, DateTimeOffset NotAfter);
