using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Creates the appropriate IAgenticAnalyzer for an AgentConfig.
/// Mirrors IAgentProviderFactory + ILlmClientFactory. Throws
/// NotSupportedException for providers that don't yet implement the
/// agentic-analyzer abstraction (currently: ollama).
/// </summary>
public interface IAgenticAnalyzerFactory
{
    IAgenticAnalyzer Create(AgentConfig config);
}
