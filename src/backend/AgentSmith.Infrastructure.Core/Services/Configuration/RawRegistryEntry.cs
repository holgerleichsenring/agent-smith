namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for one entry inside the top-level `registries:` list
/// (p0191). Token carries the operator's reference syntax (e.g.
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
