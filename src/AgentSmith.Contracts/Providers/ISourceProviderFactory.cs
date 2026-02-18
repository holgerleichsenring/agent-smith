using AgentSmith.Contracts.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Creates the appropriate ISourceProvider based on configuration.
/// </summary>
public interface ISourceProviderFactory
{
    ISourceProvider Create(SourceConfig config);
}
