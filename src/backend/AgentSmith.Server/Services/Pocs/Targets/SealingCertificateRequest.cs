namespace AgentSmith.Server.Services.Pocs.Targets;

/// <summary>2026-11-23-933dg: what the settings form posts — an https URL to fetch the controller's certificate from, or a
/// pasted PEM to read.</summary>
internal sealed record SealingCertificateRequest(string? Url, string? Pem);
