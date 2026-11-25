namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for one entry inside a project's `registries:` list
/// (p0191; per project since 2026-11-25-9992d). Token carries the operator's reference syntax (e.g.
/// "${azure_artifacts_token}"); the loader resolves it through the secrets
/// dict before constructing the public
/// <see cref="AgentSmith.Contracts.Models.Configuration.RegistryConfig"/>.
/// </summary>
public sealed class RawRegistryEntry
{
    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
