using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Delivers pipeline output in a specific format (SARIF, Markdown, console, file).
/// Implementations are selected via keyed services based on --output parameter.
/// </summary>
public interface IOutputStrategy : ITypedProvider
{
    Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default);
}
