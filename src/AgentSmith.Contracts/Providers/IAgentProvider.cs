using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Provides AI agent capabilities (plan generation, agentic code execution).
/// </summary>
public interface IAgentProvider
{
    string ProviderType { get; }

    Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CodeChange>> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        IProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default);
}
