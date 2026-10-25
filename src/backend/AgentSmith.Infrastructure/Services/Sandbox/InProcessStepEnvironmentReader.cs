using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7h: the step environment for a sandbox about to be created, from the
/// composition's configuration (its agents' api_key_secret names) and the secret values known
/// now. With no configuration loaded it still strips every declared name.
/// </summary>
public sealed class InProcessStepEnvironmentReader(ISecretValues secrets, AgentSmithConfig config)
{
    public InProcessStepEnvironment Read() => InProcessStepEnvironment.For(config, secrets.All());
}
