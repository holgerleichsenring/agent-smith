using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Creates per-project ILlmClient instances based on agent configuration.
/// Mirrors IAgentProviderFactory: each project gets its own client with
/// project-specific retry policy, model routing, and API key.
/// </summary>
public interface ILlmClientFactory
{
    ILlmClient Create(AgentConfig config);
}
