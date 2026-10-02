namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-01-7f7aa: one entry of the <c>design_sources:</c> catalog as a project carries it.
/// <see cref="SecretName"/> is the NAME of the secret holding the access token — the value is
/// looked up through <c>ISecretValues</c> at the moment of use, so no record ever holds it.
/// </summary>
public sealed record DesignSource(
    string Name,
    DesignSourceVendor Vendor,
    string SecretName,
    string? DisplayName = null);
